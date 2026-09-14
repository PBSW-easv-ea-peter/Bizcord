# Real-Time Communication Service

Subscribes to domain events published by other Bizcord microservices via RabbitMQ and is responsible for delivering real-time updates to connected clients. Corresponds to the `Real-Time Communication Service` container in the [C4 model](../../C4%20diagram/docs/workspace.dsl).

## Responsibilities

- Subscribes to domain events on RabbitMQ via [EasyNetQ](https://easynetq.com/).
- Exposes an `IMessageClient` abstraction (`Publish`/`Subscribe`) so the underlying broker can be swapped later (e.g. for Kafka) without changing the rest of the service.

> **Known gap:** there is currently no component that pushes received messages onward to clients (e.g. via SignalR/WebSockets) — `HandleMessages` only logs what it receives. See the component diagram (`RealTimeCommunicationComponents` view) for details.

## Tech stack

- .NET 10 / ASP.NET Core Web API
- [EasyNetQ](https://easynetq.com/) 8.1.7 (RabbitMQ client)
- RabbitMQ 3 (management-alpine)

## Project structure

```
src/RealTimeCommunicationServer/
  Controllers/    Manual test endpoint (MessagesController)
  Messaging/      IMessageClient, RabbitMqMessageClient, HandleMessages
  Models/         Message contracts (e.g. PingMessage)
```

## Running locally

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/), Docker.

Start the full stack (RabbitMQ + this service) with Docker Compose:

```bash
docker compose up
```

- Service: http://localhost:8080
- RabbitMQ management UI: http://localhost:15672 (guest/guest)

Alternatively, run only RabbitMQ in Docker and the service with the .NET CLI:

```bash
docker compose up rabbitmq
cd src/RealTimeCommunicationServer
dotnet run
```

By default the service connects to RabbitMQ at `localhost`. This is controlled by the `RabbitMq:Host` configuration value (`RabbitMq__Host` environment variable), which is set to `rabbitmq` when running via Docker Compose.

## Test endpoint

`POST /api/Messages` — publishes a `PingMessage` to RabbitMQ. This is a manual test endpoint used to verify the publish/subscribe pipeline; it is not (yet) a finalized part of the domain API.

```bash
curl -X POST http://localhost:8080/api/Messages \
  -H "Content-Type: application/json" \
  -d '{"text": "hello"}'
```

## Architecture

See the C4 model at [`C4 diagram/docs/workspace.dsl`](../../C4%20diagram/docs/workspace.dsl) — container view `Containers` and component view `RealTimeCommunicationComponents`.
