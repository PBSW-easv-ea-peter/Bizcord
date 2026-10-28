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
| 2 | ChatService → RabbitMQ | Publish *hangs* (`TryPublishAsync` uses `CancellationToken.None`; EasyNetQ retries internally for ~10 s per request when the broker is gone – see the chat-microservice README) | Send requests block, threads pile up, gateway times out, client retries → duplicate messages | 3 | 9 | 3 | **81** | Fail fast: timeout on publish, log + alert on timeout. Client still gets 201, because the message is saved (at-most-once is kept) (S→3, D→1) | **9** |
| 3 | RTC | A message handler throws an exception | EasyNetQ moves the message to `EasyNetQ_Default_Error_Queue`. Push is lost silently – nobody watches that queue | 3 | 1 | 9 | **27** | Monitor the error queue depth, alert when > 0 (D→1) | **3** |
| 4 | RTC | A message crashes the RTC process (poison message) | Message is redelivered on restart → crash loop. RTC is down for all users | 1 | 9 | 3 | **27** | Quorum queue with `x-delivery-limit` + dead-letter queue, so the message is removed after N attempts (S→1) | **3** |
| 5 | API Gateway | Slow responses from a downstream service | Requests pile up in the gateway and degrade other routes. The `/chats/{chatId}` aggregate is as slow as its slowest call | 3 | 9 | 3 | **81** | Ocelot `QoSOptions` per route: timeout + circuit breaker (S→3) | **27** |
| 6 | ChatDB | Database slow or at its limit | All ChatService calls get slow; sending messages fails | 3 | 9 | 3 | **81** | Now: read replicas for reads (CQRS) + circuit breaker in front of writes that fails fast with 503; client retries with an idempotency key to avoid duplicates (S→3). Future: see below | **27** |
| 7 | Infrastructure | A server, rack or zone goes down | Services on that node are unavailable | 1 | 9 | 1 | **9** | Redundancy: replicas spread across racks/zones. Fault isolation: one swim lane per bounded context, so a failure in another team's context does not stop chat (S→3) | **3** |
| 8 | RTC | RTC cannot run as more than one instance (`PresenceTracker` is in-memory) | Mitigation #7 does not work for RTC – it stays a single point of failure. Our own design choice blocks the mitigation | 9 | 3 | 3 | **81** | Shared presence via SignalR backplane (e.g. Redis) (L→1) | **9** |
| 9 | RTC | RTC restarts | Presence is lost; pushes are missed until clients reconnect | 3 | 3 | 3 | **27** | Automatic reconnect in the client, and the client fetches missed messages via REST on reconnect (S→1) | **9** |
| | | | | | | | **441** | | **99** |

## Decisions

- **Fail fast over fail quiet (#2).** A hanging publish is a symptom of an underlying problem that we must be able to detect. The publish gets a timeout and an alert, but the request still succeeds: the message is saved, and REST is the source of truth. Returning an error would make the user retry and create a duplicate.
- **Database under pressure (#6).** Reads and writes are equally important in a chat and must both be fast, so there is no obvious traffic to drop. We choose fail fast now (circuit breaker, 503, client retry with idempotency key) and keep a write queue as a future improvement.
- **Future improvement – write-behind queue (#6).** Writes go to a queue and are saved when the database is ready (queue-based load leveling). The API then answers `202 Accepted`, and the client shows the message as *pending* until it is confirmed. This changes the REST contract (no saved message with id and `SentAt` in the response, no read-your-own-writes), so it is an architectural change, not just a resiliency policy.
- **Retries.** We add no retries of our own on client calls. EasyNetQ already retries the publish internally (that retry *is* the hang in #2). A retry on publish without an outbox can create duplicate events, and retrying against a database under pressure (#6) only adds load – so retries are only safe behind a circuit breaker and with idempotent operations (idempotency key).
- **Spreading the system (#7, #8).** Spreading services across servers only lowers risk if there are several instances of the same service. ChatService is stateless and can be replicated. RTC cannot until presence is shared.

## Priorities

Highest risk first: #2, #5, #6, #8 (81 each). Candidates for the reliability lab:

1. **#2 – timeout on publish.** In our own code, cheap, and turns a silent hang into a detectable error.
2. **#6 or #5 – circuit breaker** in front of ChatDB, or Ocelot QoS on the chat routes.

## How to test

| # | Simulation |
|---|---|
| 2 | `docker pause` the RabbitMQ container and send a message: the request must return within our timeout (not after ~10 s), and an error must be logged. |
| 3, 4 | Publish an event that makes a handler throw / crash; check that it lands in the error queue / DLQ and that the next message is still processed. |
| 5 | Add an artificial delay in a ChatService endpoint and check that the gateway times out and the circuit opens after N failures. |
| 6 | Stop or throttle Postgres and check that ChatService answers 503 quickly instead of waiting for the command timeout. |

## Review

- 2026-10-28 – first version (week 44).
