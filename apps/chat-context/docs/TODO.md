# ChatService – status og to-do

Sidst opdateret: 2026-09-28. Arbejdsmode: Study-Group (plan og godkendelse før hver fase).
Detaljeret plan med alle beslutninger: `C:\Users\pibm9\.claude\plans\kan-jeg-f-dig-crispy-pearl.md`

## Kom i gang igen
```
docker compose -f apps/chat-context/chatDB/compose.yaml up -d
docker compose -f apps/real-time-communication-microservice/docker-compose.yaml up -d rabbitmq
dotnet test Bizcord.slnx        # forventet: 71 grønne
```
**Obs: intet er committet.** `apps/chat-context/`, `docs/logging-template.json`, `docs/weeks/*` og ændringerne i `Bizcord.slnx` og `workspace.dsl` ligger kun lokalt.

---

## Hvad der er lavet (uge 38)

| Fase | Indhold | Uge 38 |
|---|---|---|
| Review | C4, domænemodel og SQL i sync. Chat og Channel er adskilte contexts | – |
| 0 | Web API-skelet (net10.0), xUnit-projekt, `Bizcord.slnx`, `/health` | – |
| 1 | Domæne: `Chat`, `ChatParticipant`, `Message`, `MessageReceipt`, value objects `ChatTitle`/`MessageContent` og invarianter | Task 01 ✅ |
| 2 | Dapper-repositories, "set til og med X" som én SQL-sætning, paging nyeste først (`before`) | – |
| 3 | Use cases (`ChatAppService`, `MessageAppService`), fejltyper (404/403/409/400), `TimeProvider` afrundet til µs | – |
| 4a | Race ved direct-chat: `direct_key` + partielt unikt index. Taberen får vinderens chat | – |
| 4b | REST API (8 endpoints), `X-User-Id`, ProblemDetails via `DomainExceptionHandler`, `ChatService.http` | Task 03 ✅ |
| 5 | Events `chat.message-sent`, `chat.participant-added`, `chat.messages-seen`. `EventTypeNames` giver sprogneutrale navne. `docs/contracts.md` | Task 02 ✅ |

Vigtige beslutninger (se planen for begrundelser):
- Ét projekt med mapperne `Domain/`, `Application/`, `Infrastructure/`, `Controllers/` og `Contracts/`.
- Dapper, ingen migrations. Schemaet er `chatDB/postgres/init/001_relational_baseline.sql`, og DB'en skal nulstilles (`down -v`) ved ændringer.
- `createdByUserId` og surrogat-id på receipts er fjernet. Opretteren af en Group er Owner, i Direct er begge Member.
- Receipts gemmer første gang, en besked blev leveret/set, og overskrives aldrig. Reglen håndhæves i domænet *og* i SQL'en (`where seen_at is null`).
- Events er fede (`MessageSent` indeholder content og modtagere). Publish sker med log-og-fortsæt (at-most-once, ingen outbox).
- DTO'er bruger strenge i stedet for domænets enums, så kontrakten ikke afhænger af domænet.

---

## Hvad der mangler

### 1. Struktureret logging efter `docs/logging-template.json` ⬅ næste
Skabelonen:
```json
{ "Timestamp", "Level",
  "Location": { "Service", "FilePath", "LineNumber", "MemberName" },
  "Tracing":  { "TraceId", "SpanId", "ParentId" },
  "Message", "Payload": { ... } }
```
Skal afklares først:
- [ ] Er skabelonen fælles for alle teams/services? (Den ligger i repo-roden under `docs/`, så det tyder på det.) Hvem ejer den?
- [ ] Custom `ConsoleFormatter` (ingen ny pakke) eller Serilog med custom formatter?
- [ ] Hvor skal logs hen: console/stdout (passer til Docker i uge 39) eller en fil?

Tekniske pointer til designet:
- **Location:** `ILogger` kender ikke kaldestedet. Det kræver egne log-extension-metoder med `[CallerFilePath]`, `[CallerLineNumber]` og `[CallerMemberName]`. `Service` kommer fra konfigurationen (`"ChatService"`).
- **Tracing:** tages fra `Activity.Current` (`TraceId`, `SpanId`, `ParentSpanId`). ASP.NET opretter en Activity pr. request.
  **På tværs af services:** trace-kontekst skal med i RabbitMQ-headers (W3C `traceparent`), så RTC kan fortsætte samme trace. Det skal også skrives ind i `contracts.md`.
- **Payload:** de strukturerede properties fra log-kaldet (fx `chatId`, `messageId`). **Ingen beskedindhold og ingen persondata i logs** (GDPR).
- Eksisterende log-steder at konvertere: `MessageClientExtensions.TryPublishAsync` (fejl ved publish). Overvej også: `DomainExceptionHandler` (4xx som Warning, 500 som Error) og use cases (Information ved oprettelse, afsendelse og markering som set).
- Test: formatterens output parses som JSON med skabelonens felter. Og TraceId skal være ens i ChatService og RTC for samme besked (fase 6).

### 2. Fase 6: RTC modtager events + ekstraopgaven
- [ ] RTC får sin egen `MessageSent`-klasse og sin egen `EventTypeNames`-mapping (`chat.message-sent`). Den må **ikke** referere ChatService.
- [ ] Ekstraopgaven: discovery og auto-registrering af message handlers i RTC's `HandleMessages` (i dag kun `PingMessage`).
- [ ] End-to-end: `POST /chats/{id}/messages` i ChatService, derefter log i RTC. **Ikke verificeret endnu** gennem en kørende ChatService.
- [ ] Opdatér C4: RTC's komponent-view og relationen `messageService -> rabbitMq`.

### 3. Uge 39: Docker (`docs/weeks/uge-39-labs.md`)
- [ ] Dockerfile til ChatService (multi-stage: sdk → publish → aspnet runtime). Repo-roden som build context, så monorepo-stierne bevares.
- [ ] Compose: ChatService + chatDB + RabbitMQ med healthchecks og `depends_on: condition: service_healthy`.
- [ ] Miljøvariabler: `RabbitMq__Host`, `ConnectionStrings__ChatDb`. Koden læser dem allerede via konfigurationen.

### 4. Parkeret (ikke MVP)
- Forlad chat / fjern deltager, og i forlængelse af det: genindtrædelse (`unique (chat_id, user_id)` + `left_at`).
- Owner-invariant ("mindst én Owner") ved rolleskift og fjernelse.
- `GET /chats` (mine chats). Står under "Senere" i `chatService.md`.
- Outbox, så events ikke går tabt, når brokeren er nede.
- Validering af `userId` mod UserService, og et `user.deleted`-event (står i `contracts.md`).
- Navngivning: C4 siger "Message Service", repoet siger "chat-context/ChatService". Vælg én.
- Hvis ChatService kører uden RabbitMQ, kan publish hænge, mens EasyNetQ forsøger at forbinde.

---

## Refleksionsspørgsmål (til eksamen)
1. Hvorfor er det i orden, at Mermaid-modellen ikke viser reglen om, at `seenAt` kun sættes én gang? Hvor lever reglen?
2. Hvorfor er `Participants` en `IReadOnlyList`, og hvilken invariant ville I kunne bryde med en `List`?
3. Batch-SQL'en springer `MessageReceipt` over. Hvilke to regler er gentaget i SQL'en, og hvad sker der, hvis kun C#-versionen ændres?
4. Race-testen: hvorfor kan domænet ikke garantere "ingen dubletter" alene? Hvem har det sidste ord?
5. Hvad sker der, hvis `DomainException` står før subklasserne i `DomainExceptionHandler`? Hvorfor fanger ingen domæne- eller service-test det?
6. "REST er sandheden, events er notifikationer": hvordan opdager en klient, at den har misset en besked?
