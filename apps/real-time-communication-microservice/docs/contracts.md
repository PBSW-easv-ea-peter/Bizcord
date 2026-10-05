# Real-Time Communication Service – kontrakt mod omverdenen

Dette er det, klienter og andre services kan regne med. Alt andet (klasser, presence-implementering) er RTC's egen sag.
Konventionerne er de samme som ChatService' (camelCase-JSON, UUID-strenge, ISO 8601 i UTC) – se [`apps/chat-context/docs/contracts.md`](../../chat-context/docs/contracts.md).

## SignalR-hub (klienter)
| Emne | Regel |
|---|---|
| Endpoint | `/hubs/chat?userId={uuid}` |
| Identitet | `userId` i query (MVP – vi stoler på den ligesom ChatService' `X-User-Id`). Erstattes af et token senere. Uden gyldigt `userId` afvises forbindelsen. |
| Retning | Kun server → klient. Beskeder sendes via ChatService' REST API, ikke via hubben. |
| Flere enheder | En bruger kan have flere forbindelser; alle får push. |

### `MessageReceived`
Pushes til hver forbundet modtager, når en besked er sendt (fra `chat.message-sent`). Afsenderen får den ikke.
```json
{ "messageId": "…", "chatId": "…", "senderUserId": "…", "content": "Hej", "sentAt": "2026-10-05T12:00:00+00:00" }
```
Offline modtagere får intet push – de henter beskeden via `GET /chats/{chatId}/messages` hos ChatService.

## Events (RabbitMQ)
Samme mekanik som ChatService: eget topic-exchange pr. logisk navn, `type`-header med samme navn, `traceparent` videreføres.

### `rtc.message-delivered`
Beskeden er pushet til mindst én forbundet modtager. `deliveredToUserIds` er kun dem, der var online – ikke alle modtagere.
Publiceres ikke, hvis ingen modtagere var online.
```json
{ "messageId": "…", "chatId": "…", "deliveredToUserIds": ["…"], "deliveredAt": "2026-10-05T12:00:00+00:00" }
```
**Betydning af "delivered":** serveren har sendt beskeden til en forbundet klient. Klienten har ikke kvitteret (der er ingen ack endnu), så en forbindelse, der dør i samme øjeblik, kan give et falsk "delivered".

### Leveringsgaranti
At-most-once, ingen garanteret rækkefølge, consumers skal være tolerante – som hos ChatService.

### Kendte consumers
| Consumer | Subscription | Bruger til |
|---|---|---|
| ChatService | `chat-service` | Sætter `deliveredAt` på receipts |

## Hvad RTC har brug for fra andre
| Fra | Hvad | Status |
|---|---|---|
| ChatService | `chat.message-sent` med `recipientUserIds` og `content` | Leveret – testet i `MessageSentConsumerContractTests` |
| ChatService | `chat.participant-added`, `chat.messages-seen` | Modtages, men logges kun (push kræver chat-medlemskab i RTC) |
