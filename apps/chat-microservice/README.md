# Chat Context (ChatService)

Håndterer direkte chats og gruppechats: deltagere, beskeder og read receipts.
Domænemodel: [docs/chatservice-domain.md](docs/chatservice-domain.md) · Ansvar og operationer: [docs/chatService.md](docs/chatService.md) · **Kontrakt (REST + events): [docs/contracts.md](docs/contracts.md)**

## Struktur
```
chatDB/                  PostgreSQL (compose + init-schema)
docs/                    Domænemodel og servicebeskrivelse
src/ChatService/         ASP.NET Core Web API
  Domain/                Entiteter, value objects, invarianter (ingen afhængigheder)
  Application/           Use cases + repository-interfaces
  Infrastructure/        Dapper-repositories, messaging
  Controllers/           REST-endpoints
  Contracts/             ChatService' egne DTO'er og events, der serialiserer kontrakten. Deles IKKE som kode;
                         den delte model er docs/contracts.md (se "Vores svar på uge 38, Task 02" der)
tests/ChatService.Tests/ xUnit
```

## Kør lokalt
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
Kræver kun, at Docker kører: Postgres og RabbitMQ startes med Testcontainers (`TestDatabase`, `TestBroker`) og ryddes op bagefter.

## Ikke implementeret (bevidst fravalgt i MVP)
- Forlad chat / fjern deltager, og i forlængelse af det: genindtrædelse (`unique (chat_id, user_id)` + `left_at`).
- Owner-invariant ("mindst én Owner") ved rolleskift og fjernelse.
- `GET /chats` (mine chats). Står under "Senere" i `docs/chatService.md`.
- Outbox, så events ikke går tabt, når brokeren er nede. I dag er leveringen at-most-once.
- Validering af `userId` mod UserService, og et `user.deleted`-event (se `docs/contracts.md`).
- Navngivning: C4 siger "Message Service", repoet siger "chat-context/ChatService".
- Uden RabbitMQ hænger publish ca. 10 sek. pr. request, mens EasyNetQ forsøger igen. EasyNetQ logger samtidig ~9 Error-linjer pr. fejlet publish.
- `MessageDeliveredConsumer` abonnerer ved opstart, så ChatService starter ikke uden RabbitMQ (compose venter på brokerens healthcheck). Retry/fallback hører til uge 44.
- `traceparent` fra `rtc.message-delivered` videreføres ikke i ChatService (RTC gør det for indgående events).
- Payload i logs med en "sikker version" af input/output-data. Det kræver en allowlist-/redaction-strategi. I dag logges kun id'er.
- Ved en uhåndteret 500 kommer der muligvis to Error-linjer (`DomainExceptionHandler` og `ExceptionHandlerMiddleware`). Ikke testet.
