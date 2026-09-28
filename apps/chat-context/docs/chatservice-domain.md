```mermaid
classDiagram
    direction LR
 
    %% Aggregat: Chat (rod) + ChatParticipant
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
 
    %% Aggregat: Message (rod) - refererer Chat via id
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
 
    %% Første gang en bruger fik leveret/så en besked - overskrives aldrig
    %% Identitet: (messageId, userId)
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

## Invarianter
- **Direct:** præcis 2 forskellige brugere, begge `Member`, ingen titel, ingen nye deltagere.
- **Group:** har en titel. Opretteren bliver `Owner`. Kun `Owner`/`Admin` kan tilføje deltagere. En bruger kan kun være deltager én gang.
- **Send:** kun aktive deltagere (`leftAt` er tom) kan sende.
- **Receipt:** `deliveredAt` og `seenAt` sættes første gang og overskrives aldrig.
- **ChatTitle:** trimmet, 1-200 tegn. **MessageContent:** ikke tom, maks. 4000 tegn.
