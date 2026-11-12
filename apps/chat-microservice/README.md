# Chat Context (ChatService)

Handles direct chats and group chats: participants, messages and read receipts.
Domain model: [docs/chatservice-domain.md](docs/chatservice-domain.md) · Responsibilities and operations: [docs/chatService.md](docs/chatService.md) · **Contract (REST + events): [docs/contracts.md](docs/contracts.md)**

Failure points and reliability measures (week 44): [../../docs/risk-analysis.md](../../docs/risk-analysis.md)

## Structure
```
chatDB/                  PostgreSQL (compose + init schema)
docs/                    Domain model and service description
src/ChatService/         ASP.NET Core Web API
  Domain/                Entities, value objects, invariants (no dependencies)
  Application/           Use cases + repository interfaces
  Infrastructure/        Dapper repositories, messaging
  Controllers/           REST endpoints
  Contracts/             ChatService's own DTOs and events that serialize the contract. NOT shared as code;
                         the shared model is docs/contracts.md (see "Our answer to week 38, Task 02" there)
tests/ChatService.Tests/ xUnit
```

## Run locally
```
docker compose -f chatDB/compose.yaml up -d
docker compose -f ../real-time-communication-microservice/docker-compose.yaml up -d rabbitmq
dotnet run --project src/ChatService
```
Swagger: http://chatservice.dev.localhost:5031/swagger · Health: `/health`

## Test
```
dotnet test tests/ChatService.Tests
```
Only requires Docker to be running: Postgres and RabbitMQ are started with Testcontainers (`TestDatabase`, `TestBroker`) and cleaned up afterwards.

## When chatDB is slow or down
All database calls go through `ChatDb` (`Infrastructure/Persistence`): short timeouts and a Polly circuit breaker, so calls fail fast with `503` instead of waiting 30 seconds and piling up.
Settings are in the `ChatDb` section (`ChatDbOptions`, the defaults are in code): `TimeoutSeconds` and `CommandTimeoutSeconds` (3) override the connection string; `FailureRatio` (0.5), `MinimumThroughput` (5), `SamplingDurationSeconds` (30) and `BreakDurationSeconds` (15) control the breaker.
A `PostgresException` (e.g. a unique violation → 409) is an answer from a working database and does not count as a failure. Tested in `ChatDbOutageTests` (pauses Postgres) and `ChatDbTests`.

## Not implemented (deliberately left out of the MVP)
- Leave chat / remove participant, and by extension: rejoining (`unique (chat_id, user_id)` + `left_at`).
- Owner invariant ("at least one Owner") on role changes and removal.
- `GET /chats` (my chats). Listed under "Later" in `docs/chatService.md`.
- Outbox, so events are not lost when the broker is down. Today delivery is at-most-once.
- Validation of `userId` against UserService, and a `user.deleted` event (see `docs/contracts.md`).
- Without RabbitMQ, publish hangs for about 10 seconds per request while EasyNetQ retries. EasyNetQ also logs ~9 Error lines per failed publish.
- `MessageDeliveredConsumer` subscribes at startup, so ChatService does not start without RabbitMQ (compose waits for the broker's healthcheck). Retry/fallback belongs to week 44.
- When chatDB is unavailable, `rtc.message-delivered` events end up in EasyNetQ's error queue and are not retried, so "delivered" is missing for those messages. Safe to replay by hand: `delivered_at` is never overwritten. An alarm on the error queue is Trello #3.
- No retry of database calls and no write-behind queue: without an idempotency key, a retried `POST message` whose first attempt was saved would create a duplicate.
- Payload in logs with a "safe version" of input/output data. This requires an allowlist/redaction strategy. Today only ids are logged.
- An unhandled 500 may produce two Error lines (`DomainExceptionHandler` and `ExceptionHandlerMiddleware`). Not tested.
