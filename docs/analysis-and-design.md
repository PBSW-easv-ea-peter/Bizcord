# Bizcord – Analysis & Design (ChatService and RTC)

## 1. Overview

Bizcord is a Discord-like chat platform split into bounded contexts (map below). This repository implements two of them:

- **ChatService** (Chat context) – direct and group chats, messages and read receipts.
- **Real-Time Communication (RTC)** – pushes new messages to users who are online.

The other contexts (User, UserAuth, Channel, Engagement) are not implemented here; we only reference users by `userId`. "NotificationHub" on the map is RabbitMQ in the implementation.

![Bizcord context map](bizcord-context-map.png)

## 2. Services and boundaries

| Service | Responsible for | NOT responsible for | Code |
| --- | --- | --- | --- |
| ChatService | Direct and group chats: participants, messages and receipts (delivered/seen). Source of truth via REST. | Channel messages (Channel Service), users and authentication, pushing to clients | [`apps/chat-context`](../apps/chat-context) |
| RTC | Pushing new messages to online users (SignalR), tracking who is online, reporting what was delivered | Storing anything, receiving messages from clients (the hub is server → client only), deciding who may read a chat | [`apps/real-time-communication-microservice`](../apps/real-time-communication-microservice) |

**Why the boundaries are here:**
- **ChatService ↔ RTC:** Storage and live delivery fail and scale differently. If RTC is down, nothing is lost – the message is saved, and clients fetch it via REST.
- **Who may receive a message:** ChatService decides. `chat.message-sent` carries `recipientUserIds` (active participants minus the sender), and RTC pushes only to those who are online among them. RTC never calls ChatService and has no access rules of its own.
- **Chat ↔ Channel:** Channels have their own server and role permissions. Keeping channel messages out of ChatService keeps those rules in the Channel context.
- **Chat ↔ User:** Users are referenced by `userId` only. Identity is trusted from a header (MVP – see section 5).

Domain rules (who may send, add participants, etc.): [chatservice-domain.md](../apps/chat-context/docs/chatservice-domain.md)

## 3. Data ownership

| Data | Owner | Stored in |
| --- | --- | --- |
| Chat, ChatParticipant, Message, MessageReceipt | ChatService | ChatDB (Postgres) |
| Presence | RTC | In memory (lost on restart) |
| User profile | User Service | UserDB |
| Channel messages | Channel Service | ChannelDB |

No shared databases. Other services reference our data by id via REST or events. Event payloads are copies: RTC forwards message content but never stores it.

## 4. How it fits together

The whole lifecycle of a group chat – REST for commands and queries, RabbitMQ for events, SignalR for push:

```mermaid
sequenceDiagram
    actor Alice
    actor Bob
    participant Chat as ChatService
    participant DB as ChatDB
    participant MQ as RabbitMQ
    participant RTC

    Alice->>Chat: POST /chats/group
    Chat->>DB: save chat (Alice = Owner)
    Alice->>Chat: POST /chats/{id}/participants (Bob)
    Chat->>DB: save participant
    Chat-)MQ: chat.participant-added
    MQ-)RTC: (logged only)

    Bob->>RTC: connect /hubs/chat?userId=bob
    Note over RTC: Bob is now online (presence, in memory)

    Alice->>Chat: POST /chats/{id}/messages
    Chat->>DB: save message + receipt for Bob
    Chat-->>Alice: 201 Created
    Chat-)MQ: chat.message-sent (after save)
    MQ-)RTC: chat.message-sent
    RTC-)Bob: MessageReceived (SignalR push)
    RTC-)MQ: rtc.message-delivered (online recipients only)
    MQ-)Chat: rtc.message-delivered
    Chat->>DB: set delivered_at (first time only)

    Bob->>Chat: POST /chats/{id}/messages/{msgId}/seen
    Chat->>DB: set seen_at up to and including msgId
    Chat-)MQ: chat.messages-seen
    MQ-)RTC: (logged only)
```

If Bob is offline, there is no push and no `delivered_at`. He fetches the message later via `GET /chats/{id}/messages`.

| Event | Producer | Consumer | Used for |
| --- | --- | --- | --- |
| `chat.message-sent` | ChatService | RTC | Push the message to online recipients |
| `chat.participant-added` | ChatService | RTC | Logged only (for now) |
| `chat.messages-seen` | ChatService | RTC | Logged only (for now) |
| `rtc.message-delivered` | RTC | ChatService | Set `delivered_at` on receipts |

**Contracts:** Events use logical names, never C# type names, and the services share no code. Each consumer has its own record with only the fields it reads and ignores the rest (tolerant reader). Contract tests on both sides catch breaking changes.

**Guarantees:** At-most-once delivery, and events are published only after the change is saved. REST is the source of truth – a lost event costs a push or a `delivered_at`, never a message.

Full contracts: [ChatService](../apps/chat-context/docs/contracts.md) · [RTC](../apps/real-time-communication-microservice/docs/contracts.md)

## 5. Decisions and known limitations

| Decision / limitation | Why | Consequence |
| --- | --- | --- |
| RTC is a separate service, not SignalR inside ChatService | Failure isolation – storage and live push fail and scale differently | Extra broker hop; `delivered_at` is set shortly after the message is saved (eventual consistency) |
| At-most-once delivery, events published after save | Simple, no outbox; REST is the source of truth | A lost event costs a push or a `delivered_at`, never a message |
| Fat events: `chat.message-sent` carries content and `recipientUserIds` | RTC can push without calling back to ChatService | Recipients are a snapshot – someone who leaves right after the send still gets the push |
| No shared code between services; tolerant-reader records | Services can deploy independently; unknown fields are ignored | A misspelled event name is not caught by the other side's tests – only by the contract docs and manual e2e |
| Identity trusted from `X-User-Id` / `?userId=` | Authentication is out of scope for the MVP | Anyone can impersonate a user; to be replaced by a validated token |
| "Delivered" means pushed to a connection, not acknowledged by the client | No client ack yet | A connection that dies at the same moment can produce a false "delivered" |
| Presence is in memory | Simplest possible | Lost on restart, and RTC can't run more than one instance without a SignalR backplane |
| `chat.participant-added` and `chat.messages-seen` are only logged by RTC | Published now so future consumers (e.g. live "seen" updates) need no change in ChatService | Events without a real consumer yet |
| No PUT/DELETE yet (edit message, leave chat, rename group) | MVP focused on the send/deliver/seen flow | `left_at` exists in the domain but can't be set via the API |
| ChatService's consumer uses EasyNetQ `IBus` directly instead of `IMessageClient` | Needed the subscription handle for clean shutdown | Swapping broker touches two classes, and the trace breaks on the RTC → ChatService hop. *Being fixed.* |

> **TODO – decide before the meeting (delete this box when done):**
> 1. **participant-added / messages-seen row:** Is the "Why" our actual reason? If there was no plan, write it honestly (e.g. "published for future use").
> 2. **IBus row:** Delete it if the week 37 fix is merged before the meeting; otherwise keep "Being fixed".
> 3. **PUT/DELETE row:** Update it if the week 38 fixes are merged before the meeting.
> 4. **Postgres + Dapper:** Not in the table. Add a row if we have a reason – expect "why not EF Core?".
> 5. **Length:** 10 rows is a lot for a 20-minute meeting. Candidates to cut: "Presence is in memory" and "Delivered means pushed".

## 6. Getting started

**Run** (only Docker is required):

```bash
docker compose up -d --build   # from the repo root
dotnet test Bizcord.slnx       # Postgres and RabbitMQ are started by Testcontainers
```

| What | Where |
| --- | --- |
| ChatService API (Swagger) | http://localhost:8000/swagger |
| RTC (SignalR hub) | http://localhost:8080/hubs/chat?userId={uuid} |
| RabbitMQ management | http://localhost:15672 (guest/guest) |
| Manual requests | [`ChatService.http`](../apps/chat-context/src/ChatService/ChatService.http) |

Don't run the compose files under `apps/` at the same time – they use the same ports.

**Where to start reading** – follow one message through the system:

1. [`MessagesController.cs`](../apps/chat-context/src/ChatService/Controllers/MessagesController.cs) – REST entry point
2. [`MessageAppService.cs`](../apps/chat-context/src/ChatService/Application/MessageAppService.cs) – saves the message, then publishes `chat.message-sent`
3. [`Chat.cs`](../apps/chat-context/src/ChatService/Domain/Chat.cs) – the domain rules (who may send, add participants)
4. [`MessageSentHandler.cs`](../apps/real-time-communication-microservice/src/RealTimeCommunicationServer/Messaging/Handlers/MessageSentHandler.cs) – RTC pushes to online recipients and publishes `rtc.message-delivered`
5. [`MessageDeliveredConsumer.cs`](../apps/chat-context/src/ChatService/Infrastructure/Messaging/MessageDeliveredConsumer.cs) – ChatService sets `delivered_at`

---

> **TODO – open questions for the whole document (discuss together, delete this box when done):**
> 1. **Context map errors – fix in Canva before the meeting?** ChatDB says "engagement information", "Chanel" typo, Chat Service labelled "channel-based chat" (contradicts section 2), POSTMessage/ReadMessage fields don't match the contracts, the `rtc.message-delivered` arrow is missing.
> 2. **Naming:** "Chat Context" (map), "Message Service" (C4), ChatService (code), `chat-context` (folder). Align them or explain in one line in section 1.
> 3. **`ChatService.http`** uses port 5031 (`dotnet run`); Docker uses 8000. Fix `@host` or note it in section 6.
> 4. **Section 5 TODO box:** the five points there.
> 5. **Task 1 coverage:** The domain part is short and links to `chatservice-domain.md`. Enough, or add 2-3 lines on direct vs group chats?
> 6. **Root `README.md` is empty.** Link to this document so the other group finds it?
> 7. **C4 model (`workspace.dsl`):** Still up to date? Link it, or is that one diagram too many?
> 8. **The other group's background:** Do they already know the Bizcord assignment? If so, section 1 can be cut to two lines.
