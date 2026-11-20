# FileService – contract with the outside world

Same conventions as [ChatService's contract](../../chat-microservice/docs/contracts.md): JSON with camelCase, UUID strings, UTC ISO 8601 timestamps, ProblemDetails for errors, logical message names, tolerant readers, no shared code.

## REST API
`X-User-Id` is required where noted (trusted in the MVP, like in ChatService).

| Method and path | Body | Response |
|---|---|---|
| `POST /files` (`X-User-Id`) | multipart/form-data, field `file` | 201 `FileResponse`. The file is `Temporary` - no quota is used until it is sent in a chat. 400 if empty or over the upload limit (100 MB) |
| `GET /files/{fileId}` | | 200 the file (with its content type and name). 404 if unknown or released |
| `GET /quota` (`X-User-Id`) | | 200 `QuotaResponse` |

```json
{ "fileId": "…", "fileName": "video.mp4", "contentType": "video/mp4", "sizeBytes": 15000000, "status": "Temporary", "uploadedAt": "…" }
{ "userId": "…", "tier": "Standard", "limitBytes": 20971520, "usedBytes": 0, "remainingBytes": 20971520 }
```

## Saga messages (RabbitMQ)
FileService is a participant in ChatService's "send a message with a file" saga ([design](../../../docs/documentation/saga-file-attachment.md)). FileService owns these names. Each has its own topic exchange with the logical name, and the `type` header has the same name. FileService subscribes as `file-service`.

Every message carries `sagaId`, which the orchestrator uses to match the reply to its saga.

### Commands FileService consumes
#### `file.reserve-quota`
Check the user's quota and deduct the file's size. Answers `file.quota-reserved` or `file.quota-rejected`. A duplicate from the same saga answers `file.quota-reserved` again without deducting twice.
```json
{ "sagaId": "…", "fileId": "…", "userId": "…" }
```

#### `file.release-quota` (compensation)
Give the quota back (if this saga reserved it) and delete the file. Idempotent, and **always** answers `file.quota-released`, also when there was nothing to release. A file attached by another saga is left alone.
```json
{ "sagaId": "…", "fileId": "…", "userId": "…" }
```

### Events FileService publishes
#### `file.quota-reserved`
The quota is deducted and the file is `Attached`. Name, type and size let the consumer show the file without calling back.
```json
{ "sagaId": "…", "fileId": "…", "userId": "…", "fileName": "video.mp4", "contentType": "video/mp4", "sizeBytes": 15000000 }
```

#### `file.quota-rejected`
Nothing was reserved. If the reason is the quota, the temporary file has already been **deleted**, so there is nothing to compensate. `reason` is human-readable and may be shown to the user.
```json
{ "sagaId": "…", "fileId": "…", "userId": "…", "reason": "Storage quota exceeded: … Upgrade to Premium for more storage." }
```

#### `file.quota-released`
```json
{ "sagaId": "…", "fileId": "…", "userId": "…" }
```

### Delivery guarantee
At-most-once, like the rest of the system: replies are published after the local transaction is committed, and a lost reply is not resent. The orchestrator is expected to time out and send `file.release-quota`, which cleans up whatever state FileService is in.

### Known consumers
| Consumer | Subscription | Messages |
|---|---|---|
| ChatService | `chat-service` | `file.quota-reserved`, `file.quota-rejected`, `file.quota-released`. Sends `file.reserve-quota` and `file.release-quota` |

## What FileService needs from others
| From | What | Status |
|---|---|---|
| UserService | The user's tier (Standard/Premium), e.g. a `user.tier-changed` event | Wanted - seed data in `user_quotas` today |
| ChatService | Who may download a file (members of the chat it was sent in) | Wanted - downloads are not access-checked today |
