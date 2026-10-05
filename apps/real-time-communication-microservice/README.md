# Real-Time Communication Service

Subscribes to domain events published by other Bizcord microservices via RabbitMQ and is responsible for delivering real-time updates to connected clients. Corresponds to the `Real-Time Communication Service` container in the [C4 model](../../C4%20diagram/docs/workspace.dsl).

## Responsibilities

- Subscribes to domain events on RabbitMQ via [EasyNetQ](https://easynetq.com/).
- Pushes new messages to connected clients via SignalR (`/hubs/chat?userId=…`) and tracks who is online (`PresenceTracker`).
- Publishes `rtc.message-delivered` with the recipients the message was pushed to — ChatService uses it to set `deliveredAt` on receipts.
- Exposes an `IMessageClient` abstraction (`Publish`/`Subscribe`) so the underlying broker can be swapped later (e.g. for Kafka) without changing the rest of the service. `IClientNotifier` does the same for push.

The contract (hub messages and events) is in [`docs/contracts.md`](docs/contracts.md).

> **Not yet implemented:** push of `chat.participant-added` / `chat.messages-seen` (requires chat membership in RTC), client acknowledgement of delivery, token authentication on the hub, and a SignalR backplane for running more than one instance (presence is in-memory).

## Tech stack

- .NET 10 / ASP.NET Core Web API
- [EasyNetQ](https://easynetq.com/) 8.1.7 (RabbitMQ client)
- RabbitMQ 3 (management-alpine)

## Project structure

```
src/RealTimeCommunicationServer/
  Contracts/      Own view of ChatService' events + RTC's own (MessageDelivered, MessageReceived)
  Controllers/    Manual test endpoint (MessagesController)
  Messaging/      IMessageClient, RabbitMqMessageClient, HandleMessages, handlers
  Models/         Message contracts (e.g. PingMessage)
  Realtime/       ChatHub, PresenceTracker, IClientNotifier, QueryStringUserIdProvider
tests/RealTimeCommunicationServer.Tests/
  Unit, consumer contract and service tests. Service tests start RabbitMQ with Testcontainers - only Docker needs to run.
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
