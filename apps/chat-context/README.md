# Chat Context (ChatService)

Handles direct chats and group chats: participants, messages and read receipts.
Domain model: [docs/chatservice-domain.md](docs/chatservice-domain.md) · Responsibilities and operations: [docs/chatService.md](docs/chatService.md) · **Contract (REST + events): [docs/contracts.md](docs/contracts.md)**

## Structure
```
chatDB/                  PostgreSQL (compose + init schema)
docs/                    Domain model and service description
src/ChatService/         ASP.NET Core Web API
  Domain/                Entities, value objects, invariants (no dependencies)
  Application/           Use cases + repository interfaces
  Infrastructure/        Dapper repositories, messaging
  Controllers/           REST endpoints
  Contracts/             DTOs and events shared with other services
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

## Not implemented (deliberately left out of the MVP)
- Leave chat / remove participant, and by extension: rejoining (`unique (chat_id, user_id)` + `left_at`).
- Owner invariant ("at least one Owner") on role changes and removal.
- `GET /chats` (my chats). Listed under "Later" in `docs/chatService.md`.
- Outbox, so events are not lost when the broker is down. Today delivery is at-most-once.
- Validation of `userId` against UserService, and a `user.deleted` event (see `docs/contracts.md`).
- Naming: C4 says "Message Service", the repo says "chat-context/ChatService".
- Without RabbitMQ, publish hangs for about 10 seconds per request while EasyNetQ retries. EasyNetQ also logs ~9 Error lines per failed publish.
- `MessageDeliveredConsumer` subscribes at startup, so ChatService does not start without RabbitMQ (compose waits for the broker's healthcheck). Retry/fallback belongs to week 44.
- `traceparent` from `rtc.message-delivered` is not propagated in ChatService (RTC does this for incoming events).
- Payload in logs with a "safe version" of input/output data. This requires an allowlist/redaction strategy. Today only ids are logged.
- An unhandled 500 may produce two Error lines (`DomainExceptionHandler` and `ExceptionHandlerMiddleware`). Not tested.
