# ChatService – contract with the outside world

This is what other services can rely on. Everything else (tables, classes, language) is ChatService's own business and can change without notice.

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
| `GET /chats/{chatId}/messages?before={messageId}&limit={1-100}` | | 200, newest first |
| `POST /chats/{chatId}/messages/{messageId}/seen` | | 204 – all other users' messages *up to and including* this one |
| `GET /chats/{chatId}/messages/{messageId}/receipts` | | 200 |

Error codes: `400` invalid input · `403` not an (active) participant or missing role · `404` unknown chat/message · `409` already exists.

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
| Real-Time Communication (RTC) | `real-time-server` | all three | Pushing `chat.message-sent` to connected clients (the other two are only logged) |

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
