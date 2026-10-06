# Uge 40 – Review-guide

Guide til review af uge 40-arbejdet (2026-10-05), før det committes: Testcontainers, RTC-push og delivered-loopet.

## Contents

- [Overblik](#overblik)
- [Hvad er lavet](#hvad-er-lavet)
- [Hvad skal nærlæses](#hvad-skal-nærlæses)
- [Sådan tester du det](#sådan-tester-du-det)
  - [Automatisk](#automatisk)
  - [Mutationstjek](#mutationstjek)
  - [Manuelt hele vejen igennem](#manuelt-hele-vejen-igennem)
- [Efter review](#efter-review)

## Overblik

```
POST /chats/{id}/messages → ChatService → chat.message-sent
  → RTC MessageSentHandler: hvem af modtagerne er online (PresenceTracker)?
      → push "MessageReceived" via SignalR til dem
      → publicér rtc.message-delivered { messageId, chatId, deliveredToUserIds, deliveredAt }
  → ChatService MessageDeliveredConsumer → receipts.delivered_at (sættes kun første gang)
  → GET .../receipts viser deliveredAt
```

"Delivered" betyder, at serveren har pushet til en forbundet klient. Klienten har *ikke* kvitteret.

Se ændringerne med `git status` og `git diff`. `.dockerignore` (fjernede merge-markører) er ikke en del af dette arbejde.

## Hvad er lavet

Review i tre klumper. Start med [C4-diffen](../../C4%20diagram/docs/workspace.dsl) og [RTC's kontrakt](../../apps/real-time-communication-microservice/docs/contracts.md) for at få overblikket. Gå derefter B → C → A → tests.

| Klump | Kort | Filer |
|---|---|---|
| **A. Testcontainers** | Testene starter selv Postgres og RabbitMQ, og RabbitMQ konfigureres via `ConnectionStrings:RabbitMq` (fallback `RabbitMq:Host`) | [`TestDatabase.cs`](../../apps/chat-microservice/tests/ChatService.Tests/Persistence/TestDatabase.cs), [`TestBroker.cs`](../../apps/chat-microservice/tests/ChatService.Tests/Messaging/TestBroker.cs), [`ChatService.Tests.csproj`](../../apps/chat-microservice/tests/ChatService.Tests/ChatService.Tests.csproj) (kopierer init-SQL), [`ChatApiFactory.cs`](../../apps/chat-microservice/tests/ChatService.Tests/Api/ChatApiFactory.cs), [`RtcApiFactory.cs`](../../apps/real-time-communication-microservice/tests/RealTimeCommunicationServer.Tests/RtcApiFactory.cs), [`MessagingServiceCollectionExtensions.cs`](../../apps/chat-microservice/src/ChatService/Infrastructure/Messaging/MessagingServiceCollectionExtensions.cs), begge `Program.cs` |
| **B. RTC-push** | SignalR-hub, presence, push og publicering af `rtc.message-delivered` | [`Realtime/`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Realtime/) (4 filer), [`RtcEvents.cs`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Contracts/RtcEvents.cs), [`MessageSentHandler.cs`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Messaging/Handlers/MessageSentHandler.cs), [`HandleMessages.cs`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Messaging/HandleMessages.cs), [`EventTypeNames.cs`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Messaging/EventTypeNames.cs), [`Program.cs`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Program.cs) |
| **C. ChatService-loop** | Consumeren sætter `delivered_at` | [`MessageDeliveredConsumer.cs`](../../apps/chat-microservice/src/ChatService/Infrastructure/Messaging/MessageDeliveredConsumer.cs), [`DapperReceiptRepository.cs`](../../apps/chat-microservice/src/ChatService/Infrastructure/Persistence/DapperReceiptRepository.cs) (`MarkDeliveredAsync`), [`MessageAppService.cs`](../../apps/chat-microservice/src/ChatService/Application/MessageAppService.cs), [`Events.cs`](../../apps/chat-microservice/src/ChatService/Contracts/Events.cs), [`Program.cs`](../../apps/chat-microservice/src/ChatService/Program.cs) |
| **Tests** | Nye tests | RTC: `MessageSentHandlerTests`, `PresenceTrackerTests`, `MessageSentConsumerContractTests`, `MessageSentServiceTests`. ChatService: `ProviderContractTests`, `MessageDeliveredConsumerContractTests`, `MessageDeliveredConsumerTests`, nye cases i `ReceiptRepositoryTests` og `MessageAppServiceTests` |
| **Docs** | Kontrakter og arkitektur | [RTC `contracts.md`](../../apps/real-time-communication-microservice/docs/contracts.md) (ny), begge README'er, [ChatService `contracts.md`](../../apps/chat-microservice/docs/contracts.md), [`workspace.dsl`](../../C4%20diagram/docs/workspace.dsl) |

### Testene placeret i pyramiden

| Test | Scope (Newman) | Beviser | Beviser ikke |
|---|---|---|---|
| `MessageSentHandlerTests`, `PresenceTrackerTests`, `MessageAppServiceTests` | Unit | Logikken, fx at kun dem online får beskeden | At SignalR og RabbitMQ virker |
| `ReceiptRepositoryTests` | Service (mod rigtig DB) | SQL'en: én gang, ingen afsender, rører ikke `seen_at` | Consumer-flowet |
| `*ConsumerContractTests` | Contract | Wire-JSON'en kan læses, også med ukendte felter | At den anden side *stadig* sender det |
| `ProviderContractTests` | Contract (provider-siden) | Alle felter i `MessageSent` har korrekte værdier | Wire-formatet (det dækker `EventContractSpikeTests`) |
| `MessageSentServiceTests` | Service | RTC hele vejen igennem: rigtig broker og rigtig SignalR-klient | ChatService |
| `MessageDeliveredConsumerTests` | Service | Et event på brokeren ender som `deliveredAt` i API'et | RTC |

## Hvad skal nærlæses

Fundet ved gennemlæsning efter implementeringen. **Punkt 1 og 2 var reelle fejl og er rettet** med en regressionstest hver, som er mutationstjekket. Gennemgå rettelsen.

1. **RETTET – store/små bogstaver i userId gav falsk "delivered".** `PresenceTracker` parser userId til Guid, men SignalR matcher `Clients.Users(...)` på den præcise streng. En klient med `?userId=ABC…` blev derfor regnet for online og kom med i `rtc.message-delivered`, men fik aldrig pushet. Fejlen blev bekræftet manuelt.
   *Rettelse:* [`QueryStringUserIdProvider.Normalize`](../../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Realtime/QueryStringUserIdProvider.cs) gør userId kanonisk (`Guid.TryParse` → `.ToString()`, ellers `null`, så forbindelsen afvises). Testen er `MessageSentServiceTests` med `upperCaseUserId: true`.
2. **RETTET – ChatService' consumer tjekkede ikke deltagere.** `MarkDeliveredAsync` indsatte receipts for *alle* userIds i eventet, så et forkert eller forfalsket event kunne oprette receipts for brugere uden for chatten.
   *Rettelse:* SQL'en i [`DapperReceiptRepository`](../../apps/chat-microservice/src/ChatService/Infrastructure/Persistence/DapperReceiptRepository.cs) joiner nu på aktive deltagere (`chat_participants`, `left_at is null`). Testen er `MarkDelivered_IgnoresUsersOutsideTheChat`.
3. **`PresenceTracker.Disconnected`** (`AddOrUpdate`, derefter `TryRemove` med `KeyValuePair`): overbevis dig selv om, at en connect, der lander lige imellem, ikke bliver fjernet. Hvad sker der ved en disconnect for en ukendt bruger?
4. **`ChatHub.OnConnectedAsync`** afviser med `Context.Abort()` uden at kaste fejl. Tjek, at `OnDisconnectedAsync` ikke tæller en afvist forbindelse ned. Den laver `TryParse` igen, så det burde passe.
5. **`HandleMessages` er nu en `IHostedService`**, der abonnerer i `StartAsync`. Det fjerner en race condition, hvor events i opstarten kunne gå tabt, men nu *starter RTC ikke*, hvis RabbitMQ er nede. Det samme gælder ChatService (`MessageDeliveredConsumer`). Er det acceptabelt indtil uge 44 (reliability)?
6. **Rækkefølgen i `MessageSentHandler`:** først push, så publish. Kaster pushet en fejl, kommer der intet event, og EasyNetQ flytter beskeden til error-køen. Er det den ønskede adfærd?
7. **Betydningen af "delivered"** (ingen kvittering fra klienten): står det tydeligt nok i [RTC's kontrakt](../../apps/real-time-communication-microservice/docs/contracts.md)?
8. **Testcontainers-init** er statisk og synkron (`GetAwaiter().GetResult()`) i `TestDatabase` og `TestBroker`. Det er pragmatisk, men hvis Docker ikke kører, giver det en `TypeInitializationException` i stedet for en klar fejlbesked.
9. **Små ting:**
   - Container-opsætningen findes to gange (`TestBroker` i ChatService, `RtcApiFactory` i RTC), fordi det er to testprojekter.
   - Spike-testens rå consumer filtrerer med `body.Contains(id)`. Det er løst, men tilstrækkeligt.
   - `ChatApiFactory` bruger `services.Single(...)` og fejler, hvis consumeren ikke er registreret. Det er bevidst.

## Sådan tester du det

### Automatisk

Kun Docker Desktop skal køre. Stop andre compose-stakke først for at bevise, at testene ikke afhænger af dem. Mens testene kører, skal `docker ps` kun vise `testcontainers-*`-containere.

```
dotnet test Bizcord.slnx
```

Forventet: 99 grønne (7 Logging + 80 ChatService + 12 RTC).

Fokuseret pr. klump:

```
# B – unit
dotnet test Bizcord.slnx --filter "FullyQualifiedName~MessageSentHandlerTests|FullyQualifiedName~PresenceTrackerTests"

# B – rigtig broker + SignalR
dotnet test Bizcord.slnx --filter "FullyQualifiedName~MessageSentServiceTests"

# C – repository og consumer
dotnet test Bizcord.slnx --filter "FullyQualifiedName~ReceiptRepositoryTests|FullyQualifiedName~MessageDelivered"

# Alle kontrakttests
dotnet test Bizcord.slnx --filter "FullyQualifiedName~ContractTests"
```

### Mutationstjek

Beviser, at testene kan fejle. Ændr én ting, kør den relevante test, se den blive rød, og fortryd ændringen manuelt. Brug **ikke** `git checkout -- <fil>`, så længe filerne har andre ucommittede ændringer, for så mister du dem.

| Ændring | Forventet rød test |
|---|---|
| `MessageSentHandler.cs`: send `message.RecipientUserIds` i stedet for `online` til `PushMessageAsync` | `Handle_OnlineRecipients_PushesAndPublishesMessageDelivered` |
| `DapperReceiptRepository.cs`: fjern `and m.sender_user_id <> u.user_id` | `MarkDelivered_SkipsSenderAndKeepsFirstTimestamp` |
| `DapperReceiptRepository.cs`: fjern `where message_receipts.delivered_at is null` | `MarkDelivered_SkipsSenderAndKeepsFirstTimestamp` |
| RTC's `EventTypeNames.cs`: stav `rtc.message-delivered` forkert | `MessageSentServiceTests` timer ud |

**Tænk over** den sidste mutation: hvorfor fanger ChatService' `MessageDeliveredConsumerContractTests` den *ikke*? Svaret er, at hver side har sin egen kopi af navnet og JSON'en. Det er netop det hul, consumer-driven contracts (fx Pact) lukker.

### Manuelt hele vejen igennem

**Gennemført 2026-10-05** efter rettelse af RabbitMQ's healthcheck i [`docker-compose.yml`](../../docker-compose.yml).

> **Årsag til `.erlang.cookie: eacces`:** healthchecket kørte `rabbitmq-diagnostics` som root via `docker exec`. CLI'en oprettede `.erlang.cookie`, før serveren gjorde, så filen blev root-ejet (`0400`), og serveren, der kører som `rabbitmq`, kunne ikke læse den. Healthchecket kører nu som `rabbitmq`-brugeren. Testcontainers ramte aldrig fejlen, fordi den venter på loggen i stedet for at bruge `exec`. Har stakken fejlet før, så start den med `--renew-anon-volumes`, så den gamle cookie forsvinder.

Resultat:

| Scenarie | Push | Receipt | Som forventet? |
|---|---|---|---|
| Bob online | `MessageReceived` med content | `deliveredAt` sat, `seenAt` = null | Ja |
| Bob offline | Intet | Ingen receipt | Ja |
| Bob online med GUID i STORE bogstaver | Før rettelsen: **intet**. Efter: `MessageReceived` | `deliveredAt` sat | Før: nej (bekræftede punkt 1 under [Hvad skal nærlæses](#hvad-skal-nærlæses)). Efter: ja |

I logs har ChatService' "Message sent" og RTC's "MessageSent pushed" samme `TraceId`. ChatService' "Message marked as delivered" har ingen `TraceId`, hvilket er en kendt begrænsning (traceparent videreføres ikke i consumeren). Content forekommer 0 gange i RTC's logs.

Sådan gentager du testen:

1. Fra repo-roden: `docker compose up -d --build --renew-anon-volumes`. Tjek med `docker compose ps`, at rabbitmq er healthy, og at chatservice (`:8000`) og RTC (`:8080`) kører.
2. Forbind en klient som Bob. Der findes ingen klient i repoet, så lav en kort konsol-app (uden for repoet) med pakken `Microsoft.AspNetCore.SignalR.Client`:

   ```csharp
   using System.Text.Json;
   using Microsoft.AspNetCore.SignalR.Client;

   var bob = Guid.Parse(args[0]);
   var hub = new HubConnectionBuilder()
       .WithUrl($"http://localhost:8080/hubs/chat?userId={bob}")
       .Build();
   hub.On<JsonElement>("MessageReceived", m => Console.WriteLine($"PUSH: {m}"));

   await hub.StartAsync();
   Console.WriteLine("Forbundet - tryk Enter for at stoppe");
   Console.ReadLine();
   ```

3. Swagger på `http://localhost:8000/swagger` med `X-User-Id` = Alice:
   - `POST /chats/direct` med `{ "otherUserId": "<bob>" }`
   - `POST /chats/{id}/messages` med `{ "content": "Hej" }`
4. **Forventet:** konsollen skriver `PUSH: …`, og `GET /chats/{id}/messages/{messageId}/receipts` viser Bob med `deliveredAt` sat og `seenAt` = `null`.
5. Gentag med Bob **offline**: der kommer intet push og ingen receipt.
6. Logs: `docker compose logs realtimecommunicationserver chatservice`. Den samme `TraceId` skal gå igen i ChatService' send og i RTC's "MessageSent pushed". Content må **aldrig** optræde i RTC's logs.
7. Til punkt 1 under [Hvad skal nærlæses](#hvad-skal-nærlæses): forbind med Bobs GUID i STORE bogstaver. Efter rettelsen skal pushet komme frem som i scenarie 1.
8. Ryd op: `docker compose down`.

## Efter review

- Punkt 1 og 2 under [Hvad skal nærlæses](#hvad-skal-nærlæses) er rettet. Tag stilling til punkt 3-9.
- Commit i to dele:
  1. Testcontainers (klump A + ændringerne i spike-testen + healthcheck-rettelsen i `docker-compose.yml`)
  2. RTC-push og delivered-loop (klump B + C + tests + docs)

  Uge 40-noten, denne guide og `ProviderContractTests` kan ligge i første commit eller i deres egen.
