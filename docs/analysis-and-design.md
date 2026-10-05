# Bizcord – Analysis & Design (ChatService and RTC)

## 1. Scope

<!-- Which services does this document cover? What is deliberately left out, and why? -->

![Bizcord context map](<Bizcord (SYS).png>)

<!-- What does the diagram show? Which parts are ours? -->

## 2. Domains and bounded contexts

### 2.1 Chat context

<!-- Ubiquitous language: what do chat, participant, message and receipt mean here? -->
<!-- Direct vs group chat – what are the key rules? (link chatservice-domain.md rather than copying it) -->

### 2.2 Real-time communication

<!-- Is this a business domain or a technical capability? What does that mean for the design? -->

### 2.3 Neighbouring contexts

<!-- User, UserAuth, Channel, Engagement: one line each. What do we need from them, and what do we assume? -->

## 3. Microservice responsibilities

| Service | Responsible for | Explicitly NOT responsible for |
| --- | --- | --- |
| ChatService | | |
| RTC | | |

## 4. Service boundaries

<!-- Why is RTC a separate service and not part of ChatService? -->
<!-- Chat vs Channel: who owns channel messages? (the diagram is ambiguous) -->
<!-- Which alternatives did you consider, and why were they rejected? -->

## 5. Data ownership

| Data | Owner | Stored in | Others reference it by |
| --- | --- | --- | --- |
| | | | |

<!-- Why do we store userId and not username? -->
<!-- Does RTC own any data (e.g. presence)? Is it persisted? -->

## 6. Interactions

### 6.1 Synchronous (REST)

<!-- Who calls what? Link to the contracts instead of repeating them. -->

### 6.2 Asynchronous (events)

| Event | Producer | Consumer(s) | Why |
| --- | --- | --- | --- |
| | | | |

### 6.3 Example flow: send message → delivered

<!-- Step by step from client POST to delivered_at being set. A mermaid sequenceDiagram is an option. -->

### 6.4 Contract strategy

<!-- Answer to week 38 Task 02: why no shared code/library? What is "the shared model" then? Tolerant reader – what does it protect against, and what does it not catch? -->

### 6.5 Delivery guarantees

<!-- At-most-once, events published after save, REST as source of truth. What happens if RTC is down? -->

## 7. Decisions, trade-offs and simplifications

| Decision | Why | Consequence |
| --- | --- | --- |
| | | |

<!-- Candidates: X-User-Id without authentication, status of PUT/DELETE, Postgres + Dapper, SignalR, RabbitMQ/EasyNetQ -->

## 8. References

<!-- apps/chat-context/docs/contracts.md, apps/real-time-communication-microservice/docs/contracts.md, apps/chat-context/docs/chatservice-domain.md, C4 diagram/docs/workspace.dsl -->
