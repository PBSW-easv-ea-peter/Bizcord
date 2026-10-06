# ChatService – contract with the outside world

This is what other services can rely on. Everything else (tables, classes, language) is ChatService's own business and can change without notice.

## Our answer to week 38, Task 02 ("Implement a shared model")
The shared model is **this document**, i.e. the JSON shapes and event names below. It is not a shared C# project or a NuGet package. That is a deliberate choice:

- **No shared code.** A shared assembly couples all services to the same language, the same .NET version and the same release cadence. Every consumer then has to be updated at the same time when the model changes, and it ends up as a distributed monolith.
- **Each service has its own copy.** `src/ChatService/Contracts/` is ChatService's own serialization of the contract. Consumers write their own types with only the fields they use (*tolerant reader*). One example is RTC's `Contracts/ChatEvents.cs`, and ChatService does the same with `rtc.message-delivered` (`MessageDelivered`).
- **The contract is tested instead of shared.** The provider side is tested in `ProviderContractTests`, the consumer side in `MessageDeliveredConsumerContractTests` and RTC's `MessageSentConsumerContractTests`.
- **Internal details are hidden**, as the assignment asks. Participants' own ids, `direct_key`, the table structure and the per-message receipt rows are not exposed. Roles and chat types are strings, not enums, and "seen" is sent as one `chat.messages-seen` per action, not one event per receipt.

## General conventions
| Topic | Rule |
|---|---|
| Format | JSON, camelCase field names |
| Ids | UUID strings. **Opaque** – do not sort them or derive anything from them |
| Timestamps | ISO 8601 with offset, always UTC (e.g. `2026-09-28T12:00:00+00:00`) |
| Ordering | Expressed with timestamps (`sentAt`), never with ids |
| Enums | Strings: `Direct`/`Group` (chat type), `Member`/`Admin`/`Owner` (role) |
| Errors | ProblemDetails (RFC 9457), `application/problem+json` |

## REST API
All calls require the `X-User-Id` header (the user performing the action). It will be replaced by a UserAuth token later.

| Method and path | Body | Response |
|---|---|---|
| `POST /chats/direct` | `{ "otherUserId" }` | 201 new · 200 already exists (idempotent) |
| `POST /chats/group` | `{ "title" }` | 201 |
| `GET /chats/{chatId}` | | 200 |
| `POST /chats/{chatId}/participants` | `{ "userId" }` | 201 |
| `POST /chats/{chatId}/messages` | `{ "content" }` | 201 |
| `GET /chats/{chatId}/messages?before={messageId}&limit={1-100}` | | 200, newest first. Deleted messages are included (`content: null`) |
| `PUT /chats/{chatId}/messages/{messageId}` | `{ "content" }` | 200 – sender only. 409 if the message is deleted |
| `DELETE /chats/{chatId}/messages/{messageId}` | | 204 – sender only. Soft delete, idempotent |
| `POST /chats/{chatId}/messages/{messageId}/seen` | | 204 – all other users' messages *up to and including* this one |
| `GET /chats/{chatId}/messages/{messageId}/receipts` | | 200 |

Error codes: `400` invalid input · `403` not an (active) participant, missing role or not the sender · `404` unknown chat/message · `409` already exists or is deleted.

A message looks like this (`editedAt` and `deletedAt` are `null` until it happens, and `content` is `null` when the message is deleted):
```json
{ "id": "…", "chatId": "…", "senderUserId": "…", "content": "Hej", "sentAt": "…", "editedAt": null, "deletedAt": null }
```

## Events (RabbitMQ)
Each event has its own **topic exchange** with the logical name. The message's `type` header has the same name.
Subscribe by binding your own queue to the exchange with routing key `#`. No .NET and no shared code required.

### `chat.message-sent`
A message has been sent. `recipientUserIds` are the active participants minus the sender – enough to push without calling ChatService.
```json
{
  "messageId": "0192...", "chatId": "0192...", "senderUserId": "…",
  "content": "Hej", "sentAt": "2026-09-28T12:00:00+00:00",
  "recipientUserIds": ["…"]
}
```

### `chat.message-edited`
Same recipients as `chat.message-sent`. `content` is the new content.
```json
{ "messageId": "…", "chatId": "…", "content": "Hej igen", "editedAt": "2026-09-28T12:01:00+00:00", "recipientUserIds": ["…"] }
```

### `chat.message-deleted`
Published only the first time the message is deleted. Without content.
```json
{ "messageId": "…", "chatId": "…", "deletedAt": "2026-09-28T12:02:00+00:00", "recipientUserIds": ["…"] }
```

### `chat.participant-added`
```json
{ "chatId": "…", "userId": "…", "role": "Member", "addedByUserId": "…", "joinedAt": "2026-09-28T12:00:00+00:00" }
```

### `chat.messages-seen`
One per "seen up to and including" action (not one per message). Published only when at least one message was marked for the first time.
```json
{ "chatId": "…", "userId": "…", "upToMessageId": "…", "seenAt": "2026-09-28T12:00:00+00:00" }
```

### Delivery guarantee
- **At-most-once.** Events are published after the change is saved. If the broker is down, the error is logged and the event is lost (there is no outbox yet).
  The REST API is the source of truth. Events are notifications.
- **No guaranteed ordering** across events. Use the timestamps.
- Consumers should be **tolerant**: ignore unknown fields.

### Tracing
Each message carries the `traceparent` header ([W3C Trace Context](https://www.w3.org/TR/trace-context/), e.g. `00-<traceId>-<spanId>-01`) when it is published within a trace, e.g. an HTTP request.
Consumers should start their own span with it as parent. That way logs across services share the same `TraceId` (see `docs/logging-template.json`).

### Known consumers
| Consumer | Subscription | Events | Used for |
|---|---|---|---|
| Real-Time Communication (RTC) | `real-time-server` | `chat.message-sent`, `chat.participant-added`, `chat.messages-seen` | Pushing `chat.message-sent` to connected clients (the other two are only logged). Does not yet subscribe to `chat.message-edited`/`chat.message-deleted` |

The queue is only created once a consumer subscribes. Events published before that are lost.

### Versioning
New fields are added without notice and are non-breaking. A breaking change (removed or renamed field, changed meaning) is released under a new name, e.g. `chat.message-sent.v2`, and the old one is published in parallel during a transition period.

## What ChatService needs from others
| From | What | Status |
|---|---|---|
| UserService | Valid `userId`s. Today we trust `X-User-Id` and the ids we receive | Assumed |
| UserService | Event when a user is deleted/deactivated (e.g. `user.deleted`), so participations can be ended | Wanted |
| RTC | `rtc.message-delivered` (`messageId`, `deliveredToUserIds`, `deliveredAt`) → sets `deliveredAt` on receipts. Subscription `chat-service`. See [RTC's contract](../../real-time-communication-microservice/docs/contracts.md) | Delivered – tested in `MessageDeliveredConsumerContractTests` |
| UserAuth | Token instead of `X-User-Id` | Later |
