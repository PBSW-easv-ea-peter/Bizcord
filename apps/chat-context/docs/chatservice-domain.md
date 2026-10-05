```mermaid
classDiagram
    direction LR
 
    %% Aggregate: Chat (root) + ChatParticipant
    class Chat {
        +UUID id
        +ChatType type
        +ChatTitle? title
        +DateTime createdAt
        %% +bool isArchived (NOT mvp)
        +CreateDirect(userA, userB)$
        +CreateGroup(creator, title)$
        +AddParticipant(requestedBy, userId)
        +CanSend(userId) bool
    }
 
    class ChatParticipant {
        +UUID id
        +UUID userId
        +ParticipantRole role
        +DateTime joinedAt
        +DateTime? leftAt
    }
 
    %% Aggregate: Message (root) - references Chat by id
    class Message {
        +UUID id
        +UUID chatId
        +UUID senderUserId
        +MessageContent content
        %% +MessageType messageType (NOT mvp)
        +DateTime sentAt
        %% +DateTime? editedAt (NOT mvp)
        %% +DateTime? deletedAt (NOT mvp)
        +Send(chat, sender, content)$
    }
 
    %% The first time a user had a message delivered/saw it - never overwritten
    %% Identity: (messageId, userId)
    class MessageReceipt {
        +UUID messageId
        +UUID userId
        +DateTime? deliveredAt
        +DateTime? seenAt
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

## Invariants
- **Direct:** exactly 2 distinct users, both `Member`, no title, no new participants.
- **Group:** has a title. The creator becomes `Owner`. Only `Owner`/`Admin` can add participants. A user can only be a participant once.
- **Send:** only active participants (`leftAt` is empty) can send.
- **Receipt:** `deliveredAt` and `seenAt` are set the first time and never overwritten.
- **ChatTitle:** trimmed, 1-200 characters. **MessageContent:** not empty, max 4000 characters.
