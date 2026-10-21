# API gateway – design choices (ChatService)

Week 43, task 01: route ChatService through the course's Ocelot API gateway.

## Context

Bizcord is treated as one small part of a large system. Clients should have a single entry point and not need to know where each microservice runs. The gateway is that entry point; our group owns the chat routes in it.

The gateway is the standalone starter repository handed out by the teacher (`api-gateway`, cloned next to Bizcord). We do not push to it, so our route configuration is kept here as [`ocelot.chat.json`](ocelot.chat.json). To use it, copy it to `api-gateway/src/Configuration/ocelot.chat.json` and run `docker compose up --build` in the gateway clone. Ocelot merges the `ocelot.*.json` files into a generated `ocelot.json` at startup.

| Component | Local port |
|---|---|
| API gateway | 8080 |
| ChatService | 8081 |
| RTC | 8082 (not routed, see below) |

The gateway runs in Docker and reaches ChatService through `host.docker.internal:8081`. Inside the gateway container, `localhost` is the gateway itself.

## Route table

| Key | Upstream (client) | Methods | Downstream (ChatService) |
|---|---|---|---|
| `chats-create` | `/chats/{type}` | POST | `/chats/{type}` |
| `chat` | `/chats/{chatId}/info` | GET | `/chats/{chatId}` |
| `chat-participants` | `/chats/{chatId}/participants` | POST | `/chats/{chatId}/participants` |
| `messages` | `/chats/{chatId}/messages` | GET, POST | `/chats/{chatId}/messages` |
| `chat-message-by-id` | `/chats/{chatId}/messages/{everything}` | GET, POST, PUT, DELETE | `/chats/{chatId}/messages/{everything}` |
| *aggregate* | `/chats/{chatId}` | GET | `chat` + `messages` |

## Design choices

### 1. `/chats` both upstream and downstream

**Decision:** The public path is `/chats`, the same as ChatService's own path.

**Why:** In REST the URI identifies the resource and the HTTP method expresses the action. `/chats` is the collection, `/chats/{chatId}` one element in it – the plural/singular distinction is already carried by the path.

**Rejected alternatives:**
- `/chat` upstream, `/chats` downstream. Possible – decoupling public and internal paths is exactly what a gateway allows – but it gave no benefit here and makes the `Location` rewrite harder (see 5).
- `/chats` for reads and `/chat` for writes. Rejected because one chat would then have two addresses, the client would have to know which spelling goes with which method, and a `Location` returned from a create could be read but not updated.
- Renaming the ChatService routes to match the gateway. Rejected: the service should not change because of its gateway; the dependency would point the wrong way.

### 2. Aggregate for "GetChat"

**Decision:** `GET /chats/{chatId}` is an Ocelot aggregate that combines the chat (incl. participants) with the newest page of messages.

**Why:** There is no frontend yet, so the use case is an assumption: a client opening a chat window needs both metadata and messages. One request instead of two reduces round trips, which matters most on mobile.

**Trade-off:** Both parts come from the same service. A dedicated ChatService endpoint could do it with one database query, and would own the response shape. Gateway aggregation is most valuable across services. We kept it in the gateway because the task is about the gateway.

Consequences of the aggregate:
- Ocelot only aggregates GET requests.
- Two upstream templates should not be identical, so the chat-only route got the `/info` suffix.
- Incoming headers (here `X-User-Id`) are forwarded to every downstream call – verified.
- Query strings are forwarded too, so `?before=&limit=` also reaches the messages call.

### 3. Route keys are part of the public contract

The aggregate response uses route keys as JSON property names: `{"chat": {...}, "messages": [...]}`. The keys were renamed from `chat-get-by-id` / `chat-messages` to `chat` / `messages` so internal configuration names do not leak into the API. Renaming a key is now a breaking change for clients.

### 4. Placeholders vs. an explicit allowlist

- `/chats/{type}` covers both `direct` and `group`. This is safe because ChatService only has the two literal routes; `POST /chats/hest` is rejected by the service (405 – ASP.NET routing, before model binding).
- `/chats/{chatId}/messages/{everything}` covers edit, delete, `seen` and `receipts` in one route.

**Trade-off:** Fewer routes to maintain, but the gateway is the security boundary. With explicit routes a new endpoint only becomes public when someone deliberately adds it (an allowlist). With `{everything}`, any endpoint ChatService adds under `messages/` – including internal or admin ones – is public as soon as it is deployed. Accepted for the lab; in production we would prefer explicit routes.

### 5. Rewriting the `Location` header

`POST /chats/direct` and `/chats/group` return `201 Created` with a `Location` header built by ChatService, which pointed at `host.docker.internal:8081`. That leaks the internal topology and gives the client an address it cannot use.

```json
"DownstreamHeaderTransform": { "Location": "{DownstreamBaseUrl}, {BaseUrl}" }
```

replaces the downstream base URL with the gateway's. This only works because the path is the same upstream and downstream (choice 1); with different paths the path would also need rewriting.

### 6. RTC does not go through the gateway

RTC pushes notifications to clients over SignalR/WebSockets directly, bypassing Ocelot (see the architecture drawing). Proxying long-lived WebSocket connections through Ocelot adds complexity for little gain.

**Consequence:** RTC is a second entry point into the system and must validate the client's JWT itself. This is relevant for task 02 (authentication), where the gateway alone will not protect RTC.

## Known limitations

- **Partial failures are hidden by the aggregate.** If one downstream call fails, the client still gets `200` with the error embedded, e.g. `{"chat": {...}, "messages": {"status": 500, ...}}` (Ocelot even uses the reason phrase "cannot return from aggregate.."). If a downstream response has an empty body, the result is invalid JSON: `{"chat":,"messages":}`. Clients must inspect each part.
- **One invalid route breaks the whole gateway.** Ocelot validates the full configuration at startup and refuses to start on an error (e.g. an aggregate referencing a non-existent route key). Fail-fast is good, but in a shared gateway one team's mistake takes every team's routes down. Ownership and validation of the shared configuration (e.g. in CI) would be needed.
- **The gateway can be bypassed.** ChatService is still published on port 8081 and trusts `X-User-Id` from anyone. In a real deployment only the gateway should be able to reach the service. Task 02 moves the user id into a validated token.

## Verification

Tested 2026-10-21 with Docker (gateway, ChatService, Postgres, RabbitMQ). Each endpoint was called directly on `:8081` and through `:8080`, comparing method, path, status and body.

| Test | Expected | Result |
|---|---|---|
| `POST /chats/group` | 201, `Location` on `localhost:8080` | ✔ |
| `GET /chats/{chatId}/info` vs. direct `GET /chats/{chatId}` | Identical body | ✔ |
| `GET /chats/{chatId}` (aggregate) | `{"chat": {...}, "messages": [...]}` | ✔ |
| `POST /chats/{chatId}/messages` | 201 | ✔ |
| `GET /chats/{chatId}/messages?limit=1` | 200, query string forwarded | ✔ |
| `PUT /chats/{chatId}/messages/{messageId}` | 200, `editedAt` set | ✔ |
| `POST .../messages/{messageId}/seen` | 204 | ✔ |
| `GET .../messages/{messageId}/receipts` | 200 | ✔ |
| `POST /chats/hest` | Rejected by ChatService | ✔ (405) |
| Missing `X-User-Id` | 400 problem details passed through unchanged | ✔ |
| Aggregate when the messages call fails | – | 200 with embedded 500 (see limitations) |

Lessons from testing: always call the service directly first to know which layer fails (a 500 on messages turned out to be an outdated local database schema, not the gateway), and check `docker compose logs` when the gateway returns nothing – it had exited on a configuration error.

## Backlog

- **GetChats** – list the chats a user is in, with unread counts but no content. Needs a new ChatService endpoint (e.g. `GET /chats` using `X-User-Id`) and an unread count based on receipts. Then it can be routed directly.
- Replace `{everything}` with explicit routes.
- Consider moving GetChat into ChatService as one endpoint instead of a gateway aggregate.
