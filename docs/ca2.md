# Compulsory assignment #2 – Bizcord

Entry point for CA2 (System Integration E2026, week 48). The assignment is to finish the tasks from weeks 43–47 and document the design choices and how to run and verify each of them in `docs/`. This page maps every task to its design document, the code and the way to check it.

Earlier work: analysis and design from CA1 in [analysis-and-design.md](analysis-and-design.md). The weekly lab texts are copied to [weeks/](weeks/).

## Run it

First time: create `.env` and set up Vault as described in [README → Første gang: Vault og `.env`](../README.md#første-gang-vault-og-env). After that:

```bash
docker compose up -d --build
```

The API gateway on `http://localhost:8080` is the only way in. ChatService, FileService and RTC are not published on the host (week 45). Get a token from the gateway's faucet and send it as `Authorization: Bearer <token>`. The gateway doc shows how: [gateway-design-choices.md → Verification](documentation/gateway-design-choices.md#verification).

All automated tests:

```bash
dotnet test Bizcord.slnx
```

Only Docker needs to run. Testcontainers starts Postgres and RabbitMQ.

### Last verified

| Date | Commit | Check | Result |
|---|---|---|---|
| 2026-11-24 | `5a61994` | `dotnet test Bizcord.slnx`, integration tests included | 216/216 passed |
| 2026-11-24 | `5a61994` (PR #42) | Fresh clone, README steps, `docker compose up -d --build`, [`gateway-smoke-test.sh`](documentation/gateway-smoke-test.sh) and the saga [Try it](documentation/saga-file-attachment.md#try-it) | 46/46 checks passed; saga ends `Completed` (happy path) and `Failed` (quota) |
| 2026-11-26 | `5a61994` with PRs #41, #43 and #44 merged | `dotnet test Bizcord.slnx`, integration tests included | 224/224 passed (8 new `RtcHubGuardTests`) |

## Tasks

| Week | Task | Design choices | Code | Verify |
|---|---|---|---|---|
| 43 | Routing configuration | [gateway-design-choices.md](documentation/gateway-design-choices.md) (route table, choices 1–6) | `apps/api-gateway/src/ApiGateway/Configuration/ocelot.*.json` | [`gateway-smoke-test.sh`](documentation/gateway-smoke-test.sh), expected results in [Verification](documentation/gateway-design-choices.md#verification) |
| 43 | Authentication and authorization | [gateway-design-choices.md → Authentication and authorization](documentation/gateway-design-choices.md#authentication-and-authorization-task-02) (choices 7–9) | Faucet and JWT in `apps/api-gateway`, `RouteClaimsRequirement` in the Ocelot files | Smoke test: no token → 401, wrong role → 403, member → 200 |
| 44 | Identify failure points | [risk-analysis.md](risk-analysis.md): 9 failure modes scored L×S×D | – | – |
| 44 | Reliability measures | [risk-analysis.md → Decisions](risk-analysis.md#decisions) and [gateway QoS](documentation/gateway-design-choices.md#quality-of-service-week-44-risk-5) | Publish timeout (`RabbitMqMessageClient`), ChatDB circuit breaker (`ChatDb.cs`), gateway QoS (`apps/api-gateway/src/ApiGateway/Resilience/`) | `PublishTimeoutTests`, `ChatDbOutageTests`, `DownstreamQoSHandlerTests`; manual steps in [How to test](risk-analysis.md#how-to-test) |
| 45 | East/West security | [east-west-security.md](documentation/east-west-security.md) | `expose` instead of `ports` in `docker-compose.yml`; `JwtAuthentication.cs` in ChatService; `RtcHubGuard` in the gateway for the RTC hub WebSocket | `EastWestSecurityTests`, `RtcHubGuardTests`; from the host, `:8081`–`:8083` are refused |
| 45 | HashiCorp Vault | [vault.md](documentation/vault.md) | `VaultSecretProvider` in the gateway and ChatService, `vault/policies/secrets-reader.hcl` | Test table in [vault.md](documentation/vault.md) |
| 46 | Saga scenario | [saga-file-attachment.md](documentation/saga-file-attachment.md) (flow, failure paths, the hard question) | – | – |
| 46 | Saga implementation | [saga-file-attachment.md → Why orchestration?](documentation/saga-file-attachment.md#why-orchestration) and [Why no saga framework](documentation/saga-file-attachment.md#why-no-saga-framework-rebus) | `FileMessageSagaOrchestrator` in ChatService, `QuotaSagaParticipant` in FileService | `FileMessageSagaOrchestratorTests`, `FileSagaWiringTests`, `QuotaSagaParticipantTests`; manual: [Try it](documentation/saga-file-attachment.md#try-it) |
| 47 | Logging sidecar | [logging-sidecar.md](documentation/logging-sidecar.md) | `libs/Bizcord.Logging`, `fluentd/fluent.conf` | [Verify](documentation/logging-sidecar.md#verify): `docker compose logs -f fluentd` |
| 47 | GraphQL presentation layer | [graphql.md](documentation/graphql.md) | `ChatQuery`, `ChatMutation` in ChatService, route `/graphql` in the gateway | `GraphQLApiTests`; scenarios in [graphql.md](documentation/graphql.md#test-scenarios) |

## Contracts

Each service documents its REST API and events:

- ChatService: [contracts.md](../apps/chat-microservice/docs/contracts.md)
- FileService: [contracts.md](../apps/file-microservice/docs/contracts.md)
- Real-Time Communication: [contracts.md](../apps/real-time-communication-microservice/docs/contracts.md)

## Known limitations

Each design document has its own section. The most important across the tasks:

- FileService and RTC rely on network isolation only. They do not validate the JWT themselves. For the RTC hub WebSocket the gateway checks the token and sets `userId` from `sub`, because Ocelot skips its own checks on WebSocket routes (week 45).
- RabbitMQ traffic between services is not token-protected (week 45).
- Database passwords are in plain text in `docker-compose.yml`. Only the JWT signing key is in Vault (week 45).
- Alerts on the log lines that the reliability measures write are assumed, not configured (week 44).
- The Fluentd sidecar collects logs from ChatService, FileService and RTC only. The API gateway has no `logging: "fluentd"` label, so its logs are only in `docker compose logs api-gateway` (week 47).
- ChatService and FileService log `Cannot load library libgssapi_krb5.so.2` at startup. Npgsql probes for Kerberos, which we do not use. The message is harmless.
