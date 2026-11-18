# File Context (FileService)

Stores files that users attach to messages, and each user's storage quota (Standard 20 MB, Premium 1 GB).
Added in week 46 as the participant in the saga "a user sends a file that turns out to be too large"
(ChatService is the orchestrator). Contract (REST + saga messages): [docs/contracts.md](docs/contracts.md)

## Structure
```
fileDB/                    PostgreSQL init schema + seed (own database: database per service)
docs/                      Contract
src/FileService/           ASP.NET Core Web API
  Domain/                  StoredFile, UserQuota
  Application/             FileAppService (upload/download), QuotaSagaParticipant (saga commands)
  Infrastructure/          Dapper repository (quota as one local transaction), messaging (EasyNetQ)
  Controllers/             REST endpoints
  Contracts/               FileService's own DTOs and saga messages - not shared as code
tests/FileService.Tests/   xUnit (Testcontainers Postgres)
```

## Run
Part of the root compose: `docker compose up -d --build` → http://localhost:8083/swagger, FileDB on port 5434.

Locally: start `filedb` and `rabbitmq` from the root compose, then `dotnet run --project src/FileService`.

## Test
```
dotnet test tests/FileService.Tests
```
Only requires Docker - Postgres is started with Testcontainers.

## Not implemented
- Cleanup of `Temporary` files that are never sent.
- Access check on download (FileService doesn't know chat membership).
- Giving the quota back when a message with a file is deleted in ChatService.
- Tier from UserService - seeded in `user_quotas` today.
- Blob storage - the bytes are a `bytea` column, fine for a demo only.
