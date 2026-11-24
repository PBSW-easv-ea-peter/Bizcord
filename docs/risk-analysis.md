# Risk Analysis – Bizcord (ChatService & RTC)

Failure Mode and Effects Analysis (FMEA) of the parts of Bizcord we own (ChatService, Real-Time Communication) and the shared infrastructure they depend on (API Gateway, RabbitMQ, ChatDB). Bizcord is a large system where other teams own the other bounded contexts; the analysis assumes a production deployment, not the local docker-compose setup.

Related: [analysis-and-design.md](analysis-and-design.md), [C4 model](../C4%20diagram/docs/workspace.dsl), lab text in [weeks/uge-44-labs.md](weeks/uge-44-labs.md).

## Method

For each component we list its failure modes and their effect, and score:

| Factor | 1 | 3 | 9 |
|---|---|---|---|
| **Likelihood (L)** | Unlikely | Possible | Likely |
| **Severity (S)** | Minor inconvenience | Feature degraded | Core use case (sending/reading messages) down |
| **Detection (D)** | Easy to detect | Detected with effort | Hard to detect / silent |

**Risk = L × S × D.** Each mitigation lowers one or more factors, which gives the revised risk.

## Failure modes

| # | Component | Failure mode | Effect | L | S | D | Risk | Mitigation (lowers) | Revised |
|---|---|---|---|---|---|---|---|---|---|
| 1 | ChatService → RabbitMQ | Publish fails (broker rejects or is down) | Message is saved, but no recipient gets a push. Only visible on refresh. Logged, not alerted. | 3 | 3 | 3 | **27** | Transactional outbox: event is saved with the change and published later (L→1) | **9** |
| 2 | ChatService → RabbitMQ | Publish *hangs* (`TryPublishAsync` uses `CancellationToken.None`; EasyNetQ retries internally for ~10 s per request when the broker is gone – see the chat-microservice README) | Send requests block, threads pile up, gateway times out, client retries → duplicate messages | 3 | 9 | 3 | **81** | **Done.** Fail fast: `RabbitMqMessageClient` cancels the publish after `RabbitMq:PublishTimeout` (default 2 s) and throws `TimeoutException`; `TryPublishAsync` logs "Publish timed out." as Error. Client still gets 201, because the message is saved (at-most-once is kept) (S→3). Alert on the log line is assumed in the production setup – not configured locally (D→1) | **9** |
| 3 | RTC | A message handler throws an exception | EasyNetQ moves the message to `EasyNetQ_Default_Error_Queue`. Push is lost silently – nobody watches that queue | 3 | 1 | 9 | **27** | Monitor the error queue depth, alert when > 0 (D→1) | **3** |
| 4 | RTC | A message crashes the RTC process (poison message) | Message is redelivered on restart → crash loop. RTC is down for all users | 1 | 9 | 3 | **27** | Quorum queue with `x-delivery-limit` + dead-letter queue, so the message is removed after N attempts (S→1) | **3** |
| 5 | API Gateway | Slow responses from a downstream service | Requests pile up in the gateway and degrade other routes. The `/chats/{chatId}` aggregate is as slow as its slowest call | 3 | 9 | 3 | **81** | **Done.** `DownstreamQoSHandler` (Ocelot `DelegatingHandler` with Polly.Core) wraps each call in a 2 s timeout inside a circuit breaker, one breaker per downstream service (host:port), so a slow ChatService does not open the circuit for RTC. Timeout → `504`, open circuit → `503` with `Retry-After` without calling the service. Only connection errors, timeouts and 502/503/504 count as failures, not a 500 from one buggy endpoint. Routes opt in; upload (`POST /files`) and the SignalR hub do not, because they are long-running. Fails fast instead of piling up (S→3). The gateway logs a Warning when a circuit opens; alert on it assumed as for #2 (D→1) | **9** |
| 6 | ChatDB | Database slow or at its limit | All ChatService calls get slow; sending messages fails | 3 | 9 | 3 | **81** | Now: read replicas for reads (CQRS) + circuit breaker in front of writes that fails fast with 503; client retries with an idempotency key to avoid duplicates (S→3). Future: see below | **27** |
| 7 | Infrastructure | A server, rack or zone goes down | Services on that node are unavailable | 1 | 9 | 1 | **9** | Redundancy: replicas spread across racks/zones. Fault isolation: one swim lane per bounded context, so a failure in another team's context does not stop chat (S→3) | **3** |
| 8 | RTC | RTC cannot run as more than one instance (`PresenceTracker` is in-memory) | Mitigation #7 does not work for RTC – it stays a single point of failure. Our own design choice blocks the mitigation | 9 | 3 | 3 | **81** | Shared presence via SignalR backplane (e.g. Redis) (L→1) | **9** |
| 9 | RTC | RTC restarts | Presence is lost; pushes are missed until clients reconnect | 3 | 3 | 3 | **27** | Automatic reconnect in the client, and the client fetches missed messages via REST on reconnect (S→1) | **9** |
| | | | | | | | **441** | | **81** |

## Decisions

- **Fail fast over fail quiet (#2).** A hanging publish is a symptom of an underlying problem that we must be able to detect. The publish gets a timeout and an alert, but the request still succeeds: the message is saved, and REST is the source of truth. Returning an error would make the user retry and create a duplicate.
- **Stopped vs. paused broker (#1, #2).** The test stops the broker, because that is what makes the publish hang. A *paused* broker keeps the TCP connection open, and without publisher confirms the publish may return at once while the event is lost silently. Not tested yet – it belongs to #1 (lost events), not to #2.
- **Database under pressure (#6).** Reads and writes are equally important in a chat and must both be fast, so there is no obvious traffic to drop. We choose fail fast now (circuit breaker, 503, client retry with idempotency key) and keep a write queue as a future improvement.
- **Future improvement – write-behind queue (#6).** Writes go to a queue and are saved when the database is ready (queue-based load leveling). The API then answers `202 Accepted`, and the client shows the message as *pending* until it is confirmed. This changes the REST contract (no saved message with id and `SentAt` in the response, no read-your-own-writes), so it is an architectural change, not just a resiliency policy.
- **Gateway QoS in our own handler (#5).** Ocelot's built-in `QoSOptions` needs the `Ocelot.Provider.Polly` package, which has no stable version for Ocelot 25. We wrote a small `DelegatingHandler` with Polly.Core instead. The timeout sits inside the circuit breaker, so a call that times out counts as a failure. The breaker is per downstream service and not per route, because it is the service that is sick, not one URL.
- **Retries.** We add no retries of our own on client calls. EasyNetQ already retries the publish internally (that retry *is* the hang in #2). A retry on publish without an outbox can create duplicate events, and retrying against a database under pressure (#6) only adds load – so retries are only safe behind a circuit breaker and with idempotent operations (idempotency key).
- **Spreading the system (#7, #8).** Spreading services across servers only lowers risk if there are several instances of the same service. ChatService is stateless and can be replicated. RTC cannot until presence is shared.

## Priorities

Highest risk first: #2, #5, #6, #8 (81 each). Candidates for the reliability lab:

1. **#2 – timeout on publish.** In our own code, cheap, and turns a silent hang into a detectable error.
2. **#6 or #5 – circuit breaker** in front of ChatDB, or Ocelot QoS on the chat routes.

## How to test

| # | Simulation |
|---|---|
| 2 | `PublishTimeoutTests` (integration): own RabbitMQ container, `StopAsync` after the host has started, then send a message. Asserts 201, response < 5 s and that the message is readable via REST. Without the timeout: 10.1 s (seen failing); with it: ~1 s. |
| 3, 4 | Publish an event that makes a handler throw / crash; check that it lands in the error queue / DLQ and that the next message is still processed. |
| 5 | `DownstreamQoSHandlerTests` (unit, fake downstream): a healthy service passes through; a slow one gets `504` after the timeout; repeated `503` answers open the circuit and the next call fails fast without reaching the service; a `500` does not open it; an open circuit to one service does not stop calls to another; the circuit tries again after the break. On the full stack: stop ChatService (`docker compose stop chatservice`) and call `/chats/...` through the gateway a few times – first `502`/`504`, then `503` with `Retry-After` straight away. |
| 6 | Stop or throttle Postgres and check that ChatService answers 503 quickly instead of waiting for the command timeout. |

## Review

- 2026-10-28 – first version (week 44).
- 2026-11-01 – #2 implemented and tested (timeout on publish, separate log message). D→1 assumes an alert on the log line.
- 2026-11-24 – #5 implemented in the gateway (PR #35): timeout + circuit breaker per downstream service, 6 unit tests. Revised risk 81 → 9.
