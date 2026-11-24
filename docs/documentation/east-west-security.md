# East/West security (week 45, task 01)

Goal: only the gateway is reachable from outside, and a service that is called directly still checks who the caller is (defense in depth).

## 1. Authentication – which mechanism?
**JWT bearer tokens (HS256).** The gateway issues them (dev faucet) and signs them with `JwtSigningKey` from Vault (see [vault.md](vault.md)). A missing, expired or wrongly signed token returns **401**.

The token is validated twice:
- **Gateway** – Ocelot's `Bearer` scheme on every protected route (`AuthenticationProviderKeys`).
- **ChatService** – its own JwtBearer validation with the same key, issuer and audience (`ClockSkew = 0`). It reads the key from Vault with its own token.

## 2. Authorization – which mechanism?
**Role claim (`role`) in the token.** A valid token with the wrong role returns **403**.

| Role | Allowed |
|---|---|
| `member` | Chat REST endpoints, `/graphql`, files, quota and the SignalR hub |
| `internal-service` | `/chat/health` (service-to-service) |

- Gateway: `RouteClaimsRequirement` per route in [`Configuration/ocelot.*.json`](../../apps/api-gateway/src/ApiGateway/Configuration/).
- ChatService: policies `member` / `internal-service` (`RequireClaim("role", ...)`) on controllers, GraphQL and health checks.

**The caller is the token's `sub`, never a header.** The gateway strips incoming `X-User-Id` and sets it from `Claims[sub]` (for RTC also the `userId` query parameter). ChatService overwrites `X-User-Id` again from its own validated token (`JwtAuthentication.UseUserFromToken`), so a forged header is ignored even if the gateway is bypassed.

## 3. Implementation
| Where | What | File |
|---|---|---|
| Network | `expose` instead of `ports`: ChatService, RTC and FileService are only reachable on the Docker network | [`docker-compose.yml`](../../docker-compose.yml) |
| Gateway | Bearer + role per route; strips `X-User-Id`, `X-UserId` and `X-Role` | `Configuration/ocelot.*.json`, [`Program.cs`](../../apps/api-gateway/src/ApiGateway/Program.cs) |
| ChatService | JwtBearer + policies + user from token | [`JwtAuthentication.cs`](../../apps/chat-microservice/src/ChatService/Infrastructure/Web/JwtAuthentication.cs), [`Program.cs`](../../apps/chat-microservice/src/ChatService/Program.cs) |
| Compose | Issuer pinned to `http://api-gateway`: with `Development` the gateway issued `localhost:8080` tokens that ChatService rejected | [`docker-compose.yml`](../../docker-compose.yml) |

## 4. Verification
**Automated:** [`EastWestSecurityTests`](../../apps/chat-microservice/tests/ChatService.Tests/Api/EastWestSecurityTests.cs) (ChatService), each seen failing against a mutant:

| Test | Expected |
|---|---|
| `/health` without token | 401 |
| `/health` with member token | 403 |
| `/health` with internal-service token | 200 |
| User endpoint with internal-service token | 403 |
| Token signed with another key | 401 |
| Forged `X-User-Id` | Ignored – the token decides who acts |

**Manual, full stack** (PR #36; rows marked * spot-checked again on 24 Nov 2026):

| Call | Result |
|---|---|
| `:8081` / `:8082` / `:8083` from the host * | Refused |
| `GET /chat/health` none / member / internal-service * | 401 / 403 / 200 |
| Member: create chat, add participant, get chat, send, read, `/overview` | 201 / 201 / 200 / 201 / 200 / 200 |
| `GET /chats/{id}` without token | 401 |
| Send message with internal-service token | 403 |
| `/graphql` member / none | 200 / 401 |
| `/quota`, upload, get file | 200 / 201 / 200 |
| File-message saga via the gateway | 202, `Location` on `:8080`, status `Completed` |
| Forged `X-User-Id` (token A, header B) * | Chat created by A |
| Inside the Docker network → ChatService, no token / member | 401 / 200 |
| SignalR negotiate none / member, WebSocket upgrade | 401 / 200 / 101 |

## Not covered
- FileService and RTC do not validate the JWT themselves; they trust the gateway and rely on network isolation only.
- RabbitMQ traffic between services is not token-protected.
- A message push over the WebSocket was not tested end to end.
- Tokens come from a dev faucet, not a real identity provider.
