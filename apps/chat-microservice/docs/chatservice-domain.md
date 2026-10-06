```mermaid
classDiagram
    direction LR

    %% Aggregate: Chat (root) + ChatParticipant
    class Chat {
        +UUID id
        +ChatType type
        +ChatTitle? title
        +DateTimeOffset createdAt
        %% +bool isArchived (NOT mvp)
        +CreateDirect(userA, userB, now)$ Chat
        +CreateGroup(creator, title, now)$ Chat
        +AddParticipant(requestedBy, userId, now) ChatParticipant
        +CanSend(userId) bool
        +CanRead(userId) bool
    }

    class ChatParticipant {
        +UUID id
        +UUID userId
        +ParticipantRole role
        +DateTimeOffset joinedAt
        +DateTimeOffset? leftAt
        +bool isActive
    }

    %% Aggregate: Message (root) - references Chat by id
    class Message {
        +UUID id
        +UUID chatId
        +UUID senderUserId
        +MessageContent? content
        %% +MessageType messageType (NOT mvp)
        +DateTimeOffset sentAt
        +DateTimeOffset? editedAt
        +DateTimeOffset? deletedAt
        +bool isDeleted
        +Send(chat, sender, content, now)$ Message
        +Edit(chat, requestedBy, content, now)
        +Delete(chat, requestedBy, now) bool
    }

    %% The first time a user had a message delivered/saw it - never overwritten
    %% Identity: (messageId, userId)
    class MessageReceipt {
        +UUID messageId
        +UUID userId
        +DateTimeOffset? deliveredAt
        +DateTimeOffset? seenAt
        +Create(message, userId)$ MessageReceipt
        +MarkDelivered(at)
        +MarkSeen(at)
    }

    class ChatTitle {
<<value object>>
        +string value
    }

    class MessageContent {
<<value object>>
        +string value
    }

    class ParticipantRole {
<<enumeration>>
        Member
        Admin
        Owner
    }

    class ChatType {
<<enumeration>>
        Direct
        Group
    }

    %% MessageType enum (NOT mvp): Text, Image, File, System

    Chat "1" *-- "1..*" ChatParticipant : participants
    Message ..> Chat : chatId
    MessageReceipt ..> Message : messageId

    Chat --> ChatType
    Chat --> ChatTitle
    ChatParticipant --> ParticipantRole
    Message --> MessageContent
    %% Message --> MessageType (NOT mvp)
```

`now`/`at` are passed in from outside (from `TimeProvider`), so the domain never reads the clock itself. `Restore(...)` on each entity is only used when rehydrating from the database and is left out of the diagram.

## Invariants
- **Direct:** exactly 2 distinct users, both `Member`, no title, no new participants.
- **Group:** has a title. The creator becomes `Owner`. Only `Owner`/`Admin` can add participants. A user can only be a participant once.
- **Send:** only active participants (`leftAt` is empty) can send.
- **Read:** only active participants can read the chat, its messages and receipts (`CanRead`, currently the same rule as `CanSend`).
- **Edit/Delete:** only the sender, and only while they are an active participant. A deleted message cannot be edited. Deletion is soft: `content` is removed and `deletedAt` is set once (deleting again is a no-op).
- **Receipt:** no receipt for the sender themselves. `deliveredAt` and `seenAt` are set the first time and never overwritten.
- **ChatTitle:** trimmed, 1-200 characters. **MessageContent:** not empty, max 4000 characters.
