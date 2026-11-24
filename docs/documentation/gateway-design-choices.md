# API gateway – design choices

Week 43: route our services through an Ocelot API gateway (task 01) and protect the routes with JWT authentication and authorization (task 02). Week 44 adds timeouts and a circuit breaker in the gateway (see [Quality of service](#quality-of-service-week-44-risk-5)).

## Context

Bizcord is treated as one small part of a large system. Clients should have a single entry point and not need to know where each microservice runs. The gateway is that entry point.

The gateway started as the teacher's standalone starter repository (`api-gateway`). It now lives in this repository as [`apps/api-gateway`](../../apps/api-gateway) and runs in the root `docker-compose.yml` with the other services. That let us change its code (Vault, QoS), not only its configuration. Routes are kept per service in `apps/api-gateway/src/ApiGateway/Configuration/ocelot.<service>.json`; Ocelot merges them with `ocelot.global.json` into a generated `ocelot.json` at startup. Do not edit the generated file.

| Component | Local port | Through the gateway |
|---|---|---|
| API gateway | 8080 | – |
| Web frontend | 8084 | Its only backend ([apps/web](../../apps/web)) |
| ChatService | 8081 | Yes (`ocelot.chat.json`) |
| RTC | 8082 | Yes (`ocelot.rtc.json`) |
| FileService | 8083 | Not yet (#27) |

Inside Docker the gateway reaches ChatService by its compose service name, `chatservice:8081`. Inside the gateway container, `localhost` is the gateway itself. The RTC routes still use `host.docker.internal:8082`, which only works while RTC publishes its port (#27).

The gateway gets its JWT signing key from Vault at startup. First-time setup is in the [README](../../README.md#kør-projektet).

## Route table

| Key | Upstream (client) | Methods | Downstream | Role |
|---|---|---|---|---|
| `chats-create` | `/chats/{type}` | POST | ChatService `/chats/{type}` | `member` |
| `chat` | `/chats/{chatId}` | GET | ChatService `/chats/{chatId}` | `member` |
| `chat-participants` | `/chats/{chatId}/participants` | POST | ChatService, same path | `member` |
| `messages` | `/chats/{chatId}/messages` | GET, POST | ChatService, same path | `member` |
| `chat-message-by-id` | `/chats/{chatId}/messages/{messageId}` | PUT, DELETE | ChatService, same path | `member` |
| `message-seen` | `/chats/{chatId}/messages/{messageId}/seen` | POST | ChatService, same path | `member` |
| `message-receipts` | `/chats/{chatId}/messages/{messageId}/receipts` | GET | ChatService, same path | `member` |
| `health` | `/chat/health` | GET | ChatService `/health` | `internal-service` |
| *aggregate* | `/chats/{chatId}/overview` | GET | `chat` + `messages` | `member` |
| `rtc-messages-post` | `/rtc/messages` | POST | RTC `/api/messages` | `member` |
| `rtc-hub` | `/rtc/hubs/chat/{everything}` | all | RTC `/hubs/chat/{everything}` | `member` |

Not routed: ChatService's `/chats/{chatId}/file-messages` and `/graphql`, and all of FileService (#27).

## Design choices

### 1. `/chats` both upstream and downstream

**Decision:** The public path is `/chats`, the same as ChatService's own path.

**Why:** In REST the URI identifies the resource and the HTTP method expresses the action. `/chats` is the collection, `/chats/{chatId}` one element in it – the plural/singular distinction is already carried by the path.

**Rejected alternatives:**
- `/chat` upstream, `/chats` downstream. Possible – decoupling public and internal paths is exactly what a gateway allows – but it gave no benefit here and makes the `Location` rewrite harder (see 5).
- `/chats` for reads and `/chat` for writes. Rejected because one chat would then have two addresses, the client would have to know which spelling goes with which method, and a `Location` returned from a create could be read but not updated.
- Renaming the ChatService routes to match the gateway. Rejected: the service should not change because of its gateway; the dependency would point the wrong way.

### 2. Aggregate for "open a chat"

**Decision:** `GET /chats/{chatId}/overview` is an Ocelot aggregate that combines the chat (incl. participants) with the newest page of messages.

**Why:** A client opening a chat window needs both metadata and messages. One request instead of two reduces round trips, which matters most on mobile.

**Trade-off:** Both parts come from the same service. A dedicated ChatService endpoint could do it with one database query, and would own the response shape. Gateway aggregation is most valuable across services. We kept it in the gateway because the task is about the gateway. Our own web frontend does not use it: it calls `chat` and `messages` separately, because it pages messages on its own.

Consequences of the aggregate:
- Ocelot only aggregates GET requests.
- Two upstream templates should not be identical, so the aggregate got the `/overview` suffix and `GET /chats/{chatId}` stays the plain chat. (The first version had it the other way round: the chat route was `/chats/{chatId}/info` and the aggregate took `/chats/{chatId}`. They were swapped when the gateway moved into this repository (#16). Now the plain resource has the plain REST path, and the `Location` from a create points at the chat itself.)
- Incoming headers (here `X-User-Id`) are forwarded to every downstream call.
- Query strings are forwarded too, so `?before=&limit=` also reaches the messages call.

### 3. Route keys are part of the public contract

The aggregate response uses route keys as JSON property names: `{"chat": {...}, "messages": [...]}`. The keys were renamed from `chat-get-by-id` / `chat-messages` to `chat` / `messages` so internal configuration names do not leak into the API. Renaming a key is now a breaking change for clients.

### 4. An explicit allowlist instead of placeholders

**Decision:** Every public ChatService endpoint has its own route with its own methods. Only `/chats/{type}` keeps a placeholder.

**Why:** The gateway is the security boundary. With explicit routes, a new endpoint only becomes public when someone deliberately adds it. The first version used `/chats/{chatId}/messages/{everything}` for edit, delete, `seen` and `receipts` in one route. That was fewer routes, but anything ChatService added under `messages/` – including internal or admin endpoints – would have been public as soon as it was deployed.

- `/chats/{type}` covers both `direct` and `group`. This is safe because ChatService only has the two literal routes; `POST /chats/hest` is rejected by the service (405 – ASP.NET routing, before model binding).
- **Consequence:** endpoints that nobody routed are not reachable through the gateway. Today that is `file-messages` and `/graphql` (#27).
- The RTC hub route still uses `{everything}`, because SignalR uses several sub-paths (negotiate, transports).

### 5. Rewriting the `Location` header

`POST /chats/direct` and `/chats/group` return `201 Created` with a `Location` header built by ChatService. Through the gateway that header pointed at ChatService's own address (`chatservice:8081` inside Docker), which leaks the internal topology and gives the client an address it cannot use.

```json
"DownstreamHeaderTransform": { "Location": "{DownstreamBaseUrl}, {BaseUrl}" }
```

replaces the downstream base URL with the gateway's (`BaseUrl` in `ocelot.global.json`). This only works because the path is the same upstream and downstream (choice 1); with different paths the path would also need rewriting.

### 6. RTC through the gateway

**Decision:** RTC is routed through the gateway as well (`ocelot.rtc.json`), so the browser has a single entry point.

- `/rtc/hubs/chat/{everything}` carries the SignalR hub. The gateway adds `?userId=` from the token's `sub` (`AddQueriesToRequest`), so the client no longer chooses who it is listening as.
- `/rtc/messages` routes RTC's `POST /api/messages`.

**Not verified:** whether WebSocket upgrades pass through these routes. Ocelot proxies WebSockets only for `ws`/`wss` downstream schemes, and the route uses `http`, so SignalR may fall back to long polling. The hub route is deliberately left out of the QoS timeout for that reason.

**Open question for #21:** RTC's `POST /api/messages` is a manual test endpoint (see the C4 model), but the gateway exposes it to every `member`. Either require `internal-service` or remove the route.

## Authentication and authorization (task 02)

Every user route has the same three blocks:

```json
"AuthenticationOptions": { "AuthenticationProviderKeys": ["Bearer"], "AllowedScopes": [] },
"RouteClaimsRequirement": { "role": "member" },
"AddHeadersToRequest": { "X-User-Id": "Claims[sub] > value" }
```

The internal route (`/chat/health`) requires `role: internal-service` instead and gets no user header.

Tokens come from the gateway's local faucet (`POST /faucet`), signed with a key from Vault. The faucet is a teaching tool and does not verify a real identity: a token carries exactly the claims that were requested (#33 removed a default `internal-service` role that every token used to get).

### 7. Authorization by role, not scopes

**Decision:** All user routes require `role: member`; the internal route requires `role: internal-service`.

**Why:** Two kinds of callers (users and services) map naturally to two roles. Scopes such as `chat.read` (GET) and `chat.write` (POST/PUT/DELETE) would be more fine-grained, but scopes are set per route, and the `messages` route (GET + POST) would have to be split by method first. With the explicit routes from choice 4 that is now easy, so scopes are a possible next step.

### 8. User id from the token, not from the client

**Decision:** The gateway sets `X-User-Id` from the token's `sub` claim. ChatService is unchanged and still reads `X-User-Id`.

**Why:** The client can no longer choose who it is. It sends a token, and the gateway derives the identity from it. Keeping the existing header name means the service does not change because of its gateway (same argument as choice 1). `sub` must be a Guid, since ChatService binds the header as `Guid`.

**Verified (2026-10-21):** A request with a valid token for user `1111…` **and** a forged `X-User-Id: 2222…` header returns 200. User `2222…` gets 403 on that chat when calling ChatService directly, so the service saw the id from the token. Ocelot overwrites the client's header instead of adding a second value.

**Watch out:** The starter only strips incoming `X-UserId` and `X-Role`, not our `X-User-Id`. We are safe because every user route sets the header from the token. A new route without `AddHeadersToRequest` would pass a forged `X-User-Id` straight to ChatService. Fix: strip `X-User-Id` in the gateway as well (#27).

### 9. The aggregate inherits protection from its routes

Ocelot aggregates have no authentication of their own. Protection is enforced through the routes they combine; the `RouteClaimsRequirement` on the aggregate in `ocelot.chat.json` documents the intent but is not what protects it. We tested it halfway through the rollout, while only `chat` was protected: without a token the whole aggregate returned 401, not a partial response. Today every route in the aggregate is protected, so this does not depend on Ocelot's behaviour for mixed cases.

## Quality of service (week 44, risk 5)

**Problem:** A slow ChatService kept requests open in the gateway, and the overview aggregate was as slow as its slowest call (risk 5 in [risk-analysis.md](../risk-analysis.md)).

**Decision:** Every downstream call on an opted-in route runs through a Polly pipeline: a **circuit breaker around a timeout**.

| Setting (`DownstreamQoS` in `appsettings.json`) | Value | Why |
|---|---|---|
| `Timeout` | 2 s | Far above a normal answer, below a client's patience |
| `FailureRatio` / `MinimumThroughput` | 50 % of at least 3 calls | Two unlucky calls must not open the circuit |
| `SamplingDuration` | 10 s | Window the failure ratio is measured over |
| `BreakDuration` | 5 s | Gives the service room to recover, then one trial call (half-open) |

- **The circuit only opens on "the service is sick":** timeouts, connection errors, 502, 503 and 504. A 500 from a bug in one endpoint or a 4xx is an answer from a working service.
- **One circuit per downstream service** (host:port), so a slow ChatService does not stop calls to RTC.
- **Answers from the gateway:** 504 when the timeout hits, and 503 with `Retry-After` while the circuit is open – then ChatService is not called at all.
- **Routes opt in** with `"DelegatingHandlers": ["DownstreamQoSHandler"]`: all chat routes and `POST /rtc/messages`. The SignalR hub route does not, because long-polling requests are meant to stay open longer than the timeout. New routes must opt in too.

**Why not Ocelot's `QoSOptions`?** They need `Ocelot.Provider.Polly`, which has no stable release for Ocelot 25 (only betas built against an Ocelot beta). A `DelegatingHandler` is Ocelot's own extension point and gives the same behaviour with Polly.Core, which ChatService already uses for its ChatDB breaker. Code: [`Resilience/`](../../apps/api-gateway/src/ApiGateway/Resilience/).

**Interplay with ChatService's own breaker:** when ChatDB is down, ChatService answers 503 quickly. Three of those also open the gateway's circuit, so the gateway stops forwarding until ChatService has had time to recover.

**Verified by** `apps/api-gateway/tests/ApiGateway.Tests` (`dotnet test`): pass-through, 504 on timeout, circuit opens and fails fast, a 500 does not open it, one service's circuit does not affect another, and it tries again after the break. Both the timeout and the breaker tests were seen failing with the strategy removed. Manual check against the stack:

```bash
docker compose pause chatservice
time curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $TOKEN" localhost:8080/chats/$CHAT_ID   # 504 after ~2 s
# after 3 failures within 10 s: 503 at once, with Retry-After
docker compose unpause chatservice   # after 5 s the next call goes through again
```

## Known limitations

- **Partial failures are hidden by the aggregate.** If one downstream call fails, the client still gets `200` with the error embedded, e.g. `{"chat": {...}, "messages": {"status": 500, ...}}` (Ocelot even uses the reason phrase "cannot return from aggregate.."). If a downstream response has an empty body, the result is invalid JSON: `{"chat":,"messages":}`. Clients must inspect each part.
- **One invalid route breaks the whole gateway.** Ocelot validates the full configuration at startup and refuses to start on an error (e.g. an aggregate referencing a non-existent route key). Fail-fast is good, but in a shared gateway one team's mistake takes every team's routes down. Validation of the configuration in CI would be needed.
- **The gateway can be bypassed.** ChatService, RTC and FileService still publish their ports and trust `X-User-Id` from anyone. The JWT locks the front door, but anyone calling `:8081` directly can still claim to be any user. Fix in progress (#21): keep the services on the internal Docker network only, and let ChatService validate the JWT itself (defense in depth).
- **The gateway needs Vault at startup.** If Vault is sealed or the token is missing, the gateway exits. The rest of the stack keeps running.

## Verification

### Running the smoke test

[`gateway-smoke-test.sh`](gateway-smoke-test.sh) checks routing, authentication and authorization, and East/West security (week 45), against a running stack and prints `PASS`/`FAIL` per check.

1. Start the stack from the repo root, with Vault set up as described in the [README](../../README.md#kør-projektet): `docker compose up -d --build`.
2. From the repo root: `bash docs/documentation/gateway-smoke-test.sh` (Git Bash on Windows).

The script fetches its own `member`, `guest` and `internal-service` tokens from the faucet and creates its own chat and message, so it does not depend on existing data. ChatService is not published on the host any more (#21), so the script calls it directly from inside the compose network with a throwaway `curlimages/curl` container. It finds the network from the running gateway container, so run it from the repo root. Direct calls use a token too, because ChatService validates the JWT itself. It exits with the number of failed checks. Override the defaults with `GATEWAY`, `USER_ID`, `NETWORK` or `CHATSERVICE` if your setup differs.

### Expected results

Last full run: 2026-11-24 on a fresh clone with the README steps. All 46 checks passed. Pointing `CHATSERVICE` at a wrong port makes the 9 direct checks fail, so they cannot pass when the service is not reached. The tables show what it checks.

Routing (task 01) – each endpoint called through `:8080` and, where it matters, directly on ChatService inside Docker:

| Check | Expected |
|---|---|
| `POST /chats/group` | 201, `Location` on `localhost:8080` |
| `GET /chats/{chatId}` vs. direct `GET /chats/{chatId}` | Identical body |
| `GET /chats/{chatId}/overview` (aggregate) | `{"chat": {...}, "messages": [...]}` |
| `POST /chats/{chatId}/messages` | 201 |
| `GET /chats/{chatId}/messages?limit=1` | 200, query string forwarded |
| `PUT /chats/{chatId}/messages/{messageId}` | 200 |
| `POST .../messages/{messageId}/seen` | 204 |
| `GET .../messages/{messageId}/receipts` | 200 |
| `POST /chats/hest` | 405 from ChatService |

Authentication and authorization (task 02):

| Route | No token | Invalid token | `guest` | `internal-service` | `member` |
|---|---|---|---|---|---|
| `GET /chats/{chatId}` | 401 | 401 | 403 | 403 | 200 |
| `GET /chats/{chatId}/messages` | 401 | 401 | 403 | 403 | 200 |
| `GET /chats/{chatId}/overview` | 401 | 401 | 403 | 403 | 200 |
| `PUT .../messages/{messageId}` | 401 | 401 | 403 | 403 | 200 |
| `POST /chats/group` | – | – | 403 | – | 201 |
| `GET /chat/health` | 401 | – | – | 200 | 403 |
| `GET /chats/{chatId}` with `member` + forged `X-User-Id` | | | | | 200 as the token's user |

East/West security (week 45):

| Check | Expected |
|---|---|
| `localhost:8081`, `:8082`, `:8083` from the host | Not reachable |
| ChatService directly (inside Docker), `GET /chats/{chatId}` | No token 401, invalid 401, `guest` 403, `member` 200 |
| ChatService directly, `GET /health` | `member` 403, `internal-service` 200 |
| ChatService directly, `member` + forged `X-User-Id` | 200 as the token's user |

Lessons from testing: always call the service directly first (inside Docker, see the script's `inside` helper) to know which layer fails (a 500 on messages once turned out to be an outdated local database schema, not the gateway), and check `docker compose logs api-gateway` when the gateway returns nothing – it exits on a configuration error or when Vault is sealed.

## Backlog

- Route `file-messages`, `/graphql` and FileService, and strip `X-User-Id` in the gateway (#27).
- Decide what `POST /rtc/messages` should be: internal or removed (#21).
- **GetChats** – list the chats a user is in, with unread counts but no content. Needs a new ChatService endpoint (e.g. `GET /chats` using `X-User-Id`) and an unread count based on receipts. Then it can be routed directly.
- Consider moving the overview into ChatService as one endpoint instead of a gateway aggregate.
- Scopes per method instead of one `member` role (choice 7).
