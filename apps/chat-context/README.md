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
  Contracts/             DTO'er og events, der deles med andre services
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
