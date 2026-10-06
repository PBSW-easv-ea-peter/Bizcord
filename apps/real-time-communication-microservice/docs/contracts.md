# Real-Time Communication Service – contract with the outside world

This is what clients and other services can rely on. Everything else (classes, presence implementation) is RTC's own business.
The conventions are the same as ChatService's (camelCase JSON, UUID strings, ISO 8601 in UTC) – see [`apps/chat-microservice/docs/contracts.md`](../../chat-microservice/docs/contracts.md).

## SignalR hub (clients)
| Topic | Rule |
|---|---|
| Endpoint | `/hubs/chat?userId={uuid}` |
| Identity | `userId` in the query string (MVP – we trust it, just like ChatService's `X-User-Id`). To be replaced by a token later. Without a valid `userId` the connection is rejected. |
| Direction | Server → client only. Messages are sent via ChatService's REST API, not via the hub. |
| Multiple devices | A user can have several connections; all of them receive the push. |

### `MessageReceived`
Pushed to every connected recipient when a message has been sent (from `chat.message-sent`). The sender does not receive it.
```json
{ "messageId": "…", "chatId": "…", "senderUserId": "…", "content": "Hej", "sentAt": "2026-10-05T12:00:00+00:00" }
```
Offline recipients get no push – they fetch the message via `GET /chats/{chatId}/messages` on ChatService.

## Events (RabbitMQ)
Same mechanics as ChatService: one topic exchange per logical name, a `type` header with the same name, and `traceparent` is propagated.

### `rtc.message-delivered`
The message has been pushed to at least one connected recipient. `deliveredToUserIds` contains only those who were online – not all recipients.
Not published if no recipients were online.
```json
{ "messageId": "…", "chatId": "…", "deliveredToUserIds": ["…"], "deliveredAt": "2026-10-05T12:00:00+00:00" }
```
**Meaning of "delivered":** the server has sent the message to a connected client. The client has not acknowledged it (there is no ack yet), so a connection that dies at the same moment can produce a false "delivered".

### Delivery guarantee
At-most-once, no guaranteed ordering, consumers must be tolerant – same as ChatService.

### Known consumers
| Consumer | Subscription | Used for |
|---|---|---|
| ChatService | `chat-service` | Sets `deliveredAt` on receipts |

## What RTC needs from others
| From | What | Status |
|---|---|---|
| ChatService | `chat.message-sent` with `recipientUserIds` and `content` | Delivered – tested in `MessageSentConsumerContractTests` |
| ChatService | `chat.participant-added`, `chat.messages-seen` | Received, but only logged (push requires chat membership in RTC) |
