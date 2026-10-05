# Bizcord – project rules

## Language
- Code, comments and all documentation under `apps/`, `libs/` and the root `docs/` are in **English**.
- `docs/weeks/` holds course notes and assignment texts and stays in **Danish**.
- Test data strings (e.g. `"Hej"`) may stay Danish.

## Structure
- `apps/chat-context/` – ChatService (chats, messages, receipts). Contract: `apps/chat-context/docs/contracts.md`.
- `apps/real-time-communication-microservice/` – RTC (SignalR push, presence). Contract: `apps/real-time-communication-microservice/docs/contracts.md`.
- `libs/Bizcord.Logging/` – shared log format (`docs/logging-template.json`).
- `C4 diagram/docs/workspace.dsl` – architecture. Update it when components or relationships change.

## Running and testing
- Tests: `dotnet test Bizcord.slnx`. Only Docker needs to run – Postgres and RabbitMQ are started by Testcontainers.
- Full stack: `docker compose up -d --build` from the repo root (ChatService :8000, RTC :8080, RabbitMQ UI :15672).
  After a failed RabbitMQ start, add `--renew-anon-volumes`.
- Don't run the compose files under `apps/` at the same time as the root compose (same ports).

## Contracts and messaging
- Events use logical names (`chat.message-sent`, `rtc.message-delivered`), never C# type names. No shared code between services.
- Consumers are tolerant readers: own record with only the fields they use; ignore unknown fields.
- Changing an event or endpoint means updating the service's `docs/contracts.md` and the consumer contract tests on both sides.
- Delivery is at-most-once; events are published after the change is saved. REST is the source of truth.

## Code conventions
- Comments explain *why*, not *what*. Keep them short.
- Log ids only – never message `Content` (GDPR).
- Timestamps are UTC and truncated to microseconds (Postgres precision) – use `GetUtcNowInMicroseconds`.
- User ids are normalized to the canonical Guid string before being used as SignalR user ids.

## Tests
- Integration tests are marked `[Trait("Category", "Integration")]`.
- The broker and fakes' `Published` lists are shared between tests – always find "your" message by id, never `Assert.Single` on shared state.
- New behaviour gets a test that has been seen failing (mutation check) before it is trusted.