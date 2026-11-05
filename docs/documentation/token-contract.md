# Token contract

The contract every Bizcord service relies on when it issues or validates a JWT. It plays the same role for identity as `contracts.md` does for events: the issuer and the validators agree on it, and changing it means updating both sides.

In development, tokens are issued by the dev-only token issuer (`dev/token-issuer/`). In production, UserAuth is expected to issue tokens that follow this same contract. Validators only need a different issuer address, so they run the same code in every environment.

## Signing

| Property | Value |
|---|---|
| Algorithm | `RS256` (asymmetric) |
| Key id | `kid` in the JWT header, matching a key in the issuer's JWKS |
| Discovery | `GET /.well-known/openid-configuration` |
| Public keys | `GET /.well-known/jwks.json` (the `jwks_uri` from discovery) |

**Why asymmetric:** Only the issuer holds the private key. Gateway and services hold nothing secret. They fetch the public key and can verify tokens, but they cannot mint them. A compromised service therefore cannot forge identities (least privilege). With a shared symmetric key, every validator could also issue tokens.

**Why JWKS:** Validators fetch and cache the public keys themselves. If a token arrives with an unknown `kid`, they refetch the keys. That makes key rotation possible without redeploying the validators.

## Claims

| Claim | Value | Used for |
|---|---|---|
| `iss` | `bizcord-dev-token-issuer` (dev) | Validators reject tokens from any other issuer |
| `aud` | `bizcord` (user tokens) or `bizcord-internal` (service tokens) | Separates who a token is meant for (see below) |
| `sub` | User tokens: the user id as a Guid. Service tokens: the calling service's name | Identity. ChatService and RTC use it as the user id |
| `role` | `member` (user tokens) or `internal-service` (service tokens) | Authorization |
| `exp` | Issue time + 15 minutes | Lifetime |
| `iat` | Issue time | Diagnostics |

Validators must check signature, `iss`, `aud` and `exp` without clock skew. Claim names are kept as-is (`MapInboundClaims = false`), so `sub` and `role` are read under those exact names.

### Why `iss` is a fixed string, not a URL

The issuer is reached through different host names: `localhost` from the browser, `host.docker.internal` from the gateway, and the compose service name from ChatService and RTC. If `iss` were the issuer's URL, it would differ depending on who asked, and validation would fail somewhere. A fixed name keeps the identity stable. Each validator is configured separately with the address it uses to reach discovery.

### Why internal tokens have their own audience

`role` alone tells you *what* a token may do, but not *where* it may be used. If a `bizcord-internal` token leaks, the gateway rejects it outright because it only accepts `aud = bizcord`. A leaked service token can therefore not be used from the outside, even though it carries a valid signature. The same applies in reverse: user tokens are not accepted on internal endpoints.

## Who accepts what

| Validator / endpoint | Accepted `aud` | Required `role` |
|---|---|---|
| API gateway, `/chats/*` routes | `bizcord` | `member` |
| ChatService, all endpoints | `bizcord` | `member` |
| RTC, SignalR hub (`/hubs/chat`) | `bizcord` | `member` |
| RTC, `POST /api/Messages` | `bizcord-internal` | `internal-service` (to be decided: protect or remove) |

## Lifetime and revocation

Tokens live 15 minutes, and no introspection call is made to the issuer per request. Validation is local, so the issuer is not a runtime dependency or a single point of failure for every request.

The trade-off: a token cannot be revoked before it expires. The short lifetime limits how long a leaked token or a disabled user stays valid.

RTC closes a SignalR connection when its token expires. The client then reconnects with a fresh token, so a long-lived connection cannot outlive its token.

## Transport

- REST: `Authorization: Bearer <token>`.
- SignalR: the browser's WebSocket API cannot set headers, so the token is sent as the `access_token` query parameter. It is only read on the hub path, and query strings must not be logged.
