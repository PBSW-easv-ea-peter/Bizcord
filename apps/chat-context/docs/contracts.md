# ChatService – kontrakt mod omverdenen

Dette er det, andre services kan regne med. Alt andet (tabeller, klasser, sprog) er ChatService's egen sag og kan ændres uden varsel.

## Vores svar på uge 38, Task 02 ("Implement a shared model")
Den delte model er **dette dokument**, altså JSON-formerne og event-navnene herunder. Det er ikke et delt C#-projekt eller en NuGet-pakke. Det er et bevidst fravalg:

- **Ingen delt kode.** Et delt assembly kobler alle services til samme sprog, samme .NET-version og samme release-takt. Så skal alle consumers opdateres samtidig, når modellen ændres, og det ender som en distribueret monolit.
- **Hver service har sin egen kopi.** `src/ChatService/Contracts/` er ChatService' egen serialisering af kontrakten. Consumers skriver deres egne typer med kun de felter, de bruger (*tolerant reader*). Et eksempel er RTC's `Contracts/ChatEvents.cs`, og ChatService gør det samme med `rtc.message-delivered` (`MessageDelivered`).
- **Kontrakten testes i stedet for at deles.** Provider-siden testes i `ProviderContractTests`, consumer-siden i `MessageDeliveredConsumerContractTests` og RTC's `MessageSentConsumerContractTests`.
- **Interne detaljer er skjult**, som opgaven beder om. Deltagernes egne id'er, `direct_key`, tabelstrukturen og receipt-rækkerne pr. besked eksponeres ikke. Roller og chat-typer er strenge, ikke enums, og "set" sendes som ét `chat.messages-seen` pr. handling, ikke ét event pr. receipt.

## Generelle konventioner
| Emne | Regel |
|---|---|
| Format | JSON, camelCase-feltnavne |
| Id'er | UUID-strenge. **Uigennemsigtige** – sortér eller udled ikke noget af dem |
| Tidspunkter | ISO 8601 med offset, altid UTC (fx `2026-09-28T12:00:00+00:00`) |
| Rækkefølge | Udtrykkes med tidspunkter (`sentAt`), aldrig med id'er |
| Enums | Strenge: `Direct`/`Group` (chat-type), `Member`/`Admin`/`Owner` (rolle) |
| Fejl | ProblemDetails (RFC 9457), `application/problem+json` |

## REST API
Alle kald kræver headeren `X-User-Id` (den bruger, der udfører handlingen). Den erstattes af et UserAuth-token senere.

| Metode og sti | Body | Svar |
|---|---|---|
| `POST /chats/direct` | `{ "otherUserId" }` | 201 ny · 200 findes allerede (idempotent) |
| `POST /chats/group` | `{ "title" }` | 201 |
| `GET /chats/{chatId}` | | 200 |
| `POST /chats/{chatId}/participants` | `{ "userId" }` | 201 |
| `POST /chats/{chatId}/messages` | `{ "content" }` | 201 |
| `GET /chats/{chatId}/messages?before={messageId}&limit={1-100}` | | 200, nyeste først. Slettede beskeder er med (`content: null`) |
| `PUT /chats/{chatId}/messages/{messageId}` | `{ "content" }` | 200 – kun afsenderen. 409 hvis beskeden er slettet |
| `DELETE /chats/{chatId}/messages/{messageId}` | | 204 – kun afsenderen. Blød sletning, idempotent |
| `POST /chats/{chatId}/messages/{messageId}/seen` | | 204 – alle andres beskeder *til og med* denne |
| `GET /chats/{chatId}/messages/{messageId}/receipts` | | 200 |

Fejlkoder: `400` ugyldigt input · `403` ikke (aktiv) deltager, mangler rolle eller ikke afsender · `404` ukendt chat/besked · `409` findes allerede eller er slettet.

En besked ser sådan ud (`editedAt` og `deletedAt` er `null`, indtil det sker, og `content` er `null`, når beskeden er slettet):
```json
{ "id": "…", "chatId": "…", "senderUserId": "…", "content": "Hej", "sentAt": "…", "editedAt": null, "deletedAt": null }
```

## Events (RabbitMQ)
Hvert event har sit eget **topic-exchange** med det logiske navn. Beskedens `type`-header har samme navn.
Abonnér ved at binde jeres egen kø til exchangen med routing key `#`. Der kræves ingen .NET og ingen delt kode.

### `chat.message-sent`
En besked er sendt. `recipientUserIds` er aktive deltagere minus afsenderen – nok til at pushe uden at kalde ChatService.
```json
{
  "messageId": "0192...", "chatId": "0192...", "senderUserId": "…",
  "content": "Hej", "sentAt": "2026-09-28T12:00:00+00:00",
  "recipientUserIds": ["…"]
}
```

### `chat.message-edited`
Samme modtagere som `chat.message-sent`. `content` er det nye indhold.
```json
{ "messageId": "…", "chatId": "…", "content": "Hej igen", "editedAt": "2026-09-28T12:01:00+00:00", "recipientUserIds": ["…"] }
```

### `chat.message-deleted`
Publiceres kun første gang beskeden slettes. Uden indhold.
```json
{ "messageId": "…", "chatId": "…", "deletedAt": "2026-09-28T12:02:00+00:00", "recipientUserIds": ["…"] }
```

### `chat.participant-added`
```json
{ "chatId": "…", "userId": "…", "role": "Member", "addedByUserId": "…", "joinedAt": "2026-09-28T12:00:00+00:00" }
```

### `chat.messages-seen`
Én pr. "set til og med"-handling (ikke én pr. besked). Publiceres kun, når mindst én besked blev markeret for første gang.
```json
{ "chatId": "…", "userId": "…", "upToMessageId": "…", "seenAt": "2026-09-28T12:00:00+00:00" }
```

### Leveringsgaranti
- **At-most-once.** Events publiceres, efter ændringen er gemt. Er brokeren nede, logges fejlen, og eventet går tabt (der er ingen outbox endnu).
  REST API'et er sandheden. Events er notifikationer.
- **Ingen garanteret rækkefølge** på tværs af events. Brug tidspunkterne.
- Consumers bør være **tolerante**: ignorér ukendte felter.

### Tracing
Hver besked har headeren `traceparent` ([W3C Trace Context](https://www.w3.org/TR/trace-context/), fx `00-<traceId>-<spanId>-01`), når den publiceres inden for en trace, fx et HTTP-request.
Consumers bør starte deres egen span med den som parent. Så får logs på tværs af services samme `TraceId` (se `docs/logging-template.json`).

### Kendte consumers
| Consumer | Subscription | Events | Bruger til |
|---|---|---|---|
| Real-Time Communication (RTC) | `real-time-server` | `chat.message-sent`, `chat.participant-added`, `chat.messages-seen` | Push af `chat.message-sent` til forbundne klienter (de to andre logges kun). Abonnerer endnu ikke på `chat.message-edited`/`chat.message-deleted` |

Køen oprettes først, når en consumer abonnerer. Events, der publiceres før det, går tabt.

### Versionering
Nye felter tilføjes uden varsel og er ikke-brydende. En brydende ændring (fjernet eller omdøbt felt, ændret betydning) udgives under et nyt navn, fx `chat.message-sent.v2`, og det gamle publiceres parallelt i en overgangsperiode.

## Hvad ChatService har brug for fra andre
| Fra | Hvad | Status |
|---|---|---|
| UserService | Gyldige `userId`'er. I dag stoler vi på `X-User-Id` og de id'er, vi får | Antaget |
| UserService | Event når en bruger slettes/deaktiveres (fx `user.deleted`), så deltagelser kan afsluttes | Ønsket |
| RTC | `rtc.message-delivered` (`messageId`, `deliveredToUserIds`, `deliveredAt`) → sætter `deliveredAt` på receipts. Subscription `chat-service`. Se [RTC's kontrakt](../../real-time-communication-microservice/docs/contracts.md) | Leveret – testet i `MessageDeliveredConsumerContractTests` |
| UserAuth | Token i stedet for `X-User-Id` | Senere |
