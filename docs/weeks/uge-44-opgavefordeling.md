# Uge 44 — Opgave 2: Fordeling af reliability measures

Grundlag: [risk-analysis.md](../risk-analysis.md) (opgave 1). Vi tager de tre failure modes med risiko 81, som vi selv kan løse i denne runde. Fordelingen er lavet, så vi arbejder i hver sin del af koden og undgår merge-konflikter.

| | Peter | Sander | Ea |
|---|---|---|---|
| **Failure mode** | #2 Publish hænger | #5 Langsom downstream bag gatewayen | #6 ChatDB langsom / på limit |
| **Lag** | ChatService – `Application/MessageClientExtensions.cs` / `Infrastructure/Messaging` | API Gateway – `ocelot.chat.json` | ChatService – `Infrastructure/Persistence` + `Infrastructure/Web` |
| **Mønster** | Timeout (fail fast) | Timeout + Circuit Breaker | Circuit Breaker + Timeout |
| **Størrelse** | Lille | Lille–mellem | Mellem–stor |

Labbens tre spørgsmål skal besvares for hver del: **Hvilket lag? Hvilket mønster? Hvordan tester vi det?**

---

## Peter — #2 Timeout på publish

**Problem:** Uden RabbitMQ hænger `POST /chats/{id}/messages` i ca. 10 s, fordi EasyNetQ selv prøver igen, og `TryPublishAsync` bruger `CancellationToken.None`.

**Mål:** Requesten kommer tilbage inden for et kort timeout (fx 2 s). Klienten får stadig 201, fordi beskeden er gemt (at-most-once bevares). Timeout logges som en tydelig fejl, så den kan alarmeres på.

**Sådan:**
1. Giv publish et eget timeout i `TryPublishAsync` – fx en `CancellationTokenSource` med timeout, eller `Task.WaitAsync(timeout)`. Stadig **ikke** requestens token (et afbrudt request må ikke tabe eventet).
2. Tjek om EasyNetQ faktisk respekterer cancellation. Hvis ikke, er `WaitAsync` sikrere – men så kører publish videre i baggrunden. Overvej om det er ok.
3. Gør timeout konfigurerbart (`appsettings.json`, fx `RabbitMq:PublishTimeout`).
4. Log timeout med sin egen besked (fx "Publish timed out"), så den kan skelnes fra andre publish-fejl. Kun ids – aldrig `Content`.

**Test:**
- Integrationstest (`[Trait("Category", "Integration")]`): pause broker-containeren via Testcontainers (`PauseAsync`/`UnpauseAsync`), send en besked, og assert at svaret er 201 og kommer under timeout + margin.
- Se testen fejle uden ændringen først (mutation check).

---

## Sander — #5 QoS i gatewayen

**Problem:** Ingen routes i `ocelot.chat.json` har `QoSOptions`. En langsom ChatService holder requests åbne i gatewayen, og aggregatet `/chats/{chatId}` er lige så langsomt som det langsomste kald.

**Mål:** Gatewayen giver hurtigt en fejl, når ChatService er langsom, og stopper med at kalde den i en periode efter gentagne fejl.

**Sådan:**
Gatewayen ligger i `SI/api-gateway` (underviserens repo). Vi må gerne rette lokalt, men **må ikke pushe** til hans repo.

**0. Versionering af gateway-ændringerne (før kodning):** Polly kræver kodeændringer (`.csproj` + `ServiceExtensions.cs`), så en kopi af konfigurationen er ikke længere nok. Spørg underviseren, om en fork er ok:

- **Hvis ja – fork (foretrukket):**
  1. Fork `patrickstolc/api-gateway` til vores GitHub og giv Peter og Ea adgang.
  2. Peg den lokale klon om: `git remote rename origin upstream` og `git remote add origin <vores fork-URL>`.
  3. Commit vores routes fra uge 43 (`ocelot.chat.json`) som første commit, derefter Polly + QoS på en branch med PR.
  4. Hent underviserens opdateringer med `git fetch upstream` + `git merge upstream/main`.
  5. Henvis til forken fra Bizcord's `README.md` og `docs/documentation/gateway-design-choices.md`.
- **Hvis nej – patch-fil:**
  1. Lav ændringerne lokalt i `SI/api-gateway`.
  2. Gem dem som patch: `git diff > ../Bizcord/docs/documentation/api-gateway.patch` (husk nye filer: `git add -N <fil>` først).
  3. Commit patch-filen og `ocelot.chat.json` i Bizcord, og beskriv i `gateway-design-choices.md`, hvordan den anvendes: `git apply ../Bizcord/docs/documentation/api-gateway.patch` i en frisk klon.
  4. Opdatér patch-filen hver gang gateway-ændringerne ændres.

1. Gatewayen har i dag **ikke** Polly (kun `Ocelot` 25.0.2). Tilføj `Ocelot.Provider.Polly` i samme version som `Ocelot`, og skift i `src/Extensions/ServiceExtensions.cs`:
   `services.AddOcelot(configuration);` → `services.AddOcelot(configuration).AddPolly();`
   QoS virker ikke uden.
2. Tilføj på hver chat-route i `src/Configuration/ocelot.chat.json`, fx (tjek feltnavnene i Ocelot 25-dokumentationen – QoS blev ændret med Polly v8; overvej også om QoS kan sættes globalt i `ocelot.global.json`):
   ```json
   "QoSOptions": {
     "ExceptionsAllowedBeforeBreaking": 3,
     "DurationOfBreak": 5000,
     "TimeoutValue": 2000
   }
   ```
   Begrund tallene: timeout skal være kortere end klientens, men længere end et normalt svar.
3. Overvej aggregatet: hvad returnerer det, når ét af de to kald fejler?
4. Beskriv Polly-ændringen og valg af tal i `Bizcord/docs/documentation/gateway-design-choices.md`. Uden fork: kopiér også den færdige `ocelot.chat.json` dertil og opdatér patch-filen (se trin 0).

**Test:**
- `docker pause` ChatService-containeren og kald en route gennem gatewayen: svaret skal komme efter ca. `TimeoutValue`.
- Efter 3 fejl skal næste kald fejle med det samme (circuit åben). Efter `DurationOfBreak` skal den prøve igen.
- Udvid `Bizcord/docs/documentation/gateway-smoke-test.sh` med scenariet (gateway-repoets `tests/smoke.py` hører til underviserens repo).

---

## Ea — #6 Circuit breaker foran ChatDB

**Problem:** Er Postgres langsom, venter hvert kald på Npgsql's command timeout (default 30 s), og presset bliver kun større.

**Mål:** Kald fejler hurtigt med **503 Service Unavailable**, og efter gentagne fejl stopper ChatService med at kalde databasen i en periode (circuit breaker).

**Sådan:**
1. Sæt et kortere timeout i connection string (`Timeout` og `Command Timeout`, fx 3 s).
2. Tilføj en circuit breaker (Polly v8 `ResiliencePipeline`) omkring databasekaldene. Find ét sted at lægge den – fx omkring `NpgsqlDataSource` eller som decorator på repositories – i stedet for at gentage den i hver metode.
3. Map `BrokenCircuitException` og timeout til 503 i `DomainExceptionHandler`, evt. med `Retry-After`-header. Log som Warning/Error med ids.
4. Opdatér `apps/chat-microservice/docs/contracts.md` med 503 (kontrakten ændres).
5. Overvej `MessageDeliveredConsumer`: hvad sker der med `rtc.message-delivered`, når circuit er åben?

**Ikke med i denne runde:** idempotency key i klienten og write-behind kø (se Decisions i risikoanalysen).

**Test:**
- Integrationstest: pause Postgres-containeren, kald et endpoint N gange. De første fejler efter timeout, derefter svarer de straks 503. Unpause og vent `BreakDuration` – så virker det igen.
- Se testen fejle uden ændringen først.

**Hvis det bliver for stort:** byt til #3 – alarm på RTC's error queue.

---

## Fælles aftaler

- **Polly:** Peter og Ea rører begge ChatService. Aftal hvem der tilføjer pakken i `ChatService.csproj`, så den kun kommer ind én gang.
- **Branches:** én branch pr. person, små PR'er, review af en anden fra gruppen.
- **Når man er færdig:** opdatér sin række i `risk-analysis.md` (revideret score + "Review"-dato) og skriv kort, hvordan det blev testet.
- **Åbent punkt:** ChatService kan ikke starte uden RabbitMQ (`MessageDeliveredConsumer` subscriber ved startup). Skal den med som #10 i risikoanalysen?
