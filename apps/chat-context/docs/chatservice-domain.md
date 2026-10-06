```mermaid
classDiagram
    direction LR

    %% Aggregat: Chat (rod) + ChatParticipant
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

    %% Aggregat: Message (rod) - refererer Chat via id
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

    %% Første gang en bruger fik leveret/så en besked - overskrives aldrig
    %% Identitet: (messageId, userId)
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

`now`/`at` gives udefra (fra `TimeProvider`), så domænet ikke selv læser uret. `Restore(...)` på hver entitet bruges kun ved genopbygning fra databasen og er udeladt af diagrammet.

## Invarianter
- **Direct:** præcis 2 forskellige brugere, begge `Member`, ingen titel, ingen nye deltagere.
- **Group:** har en titel. Opretteren bliver `Owner`. Kun `Owner`/`Admin` kan tilføje deltagere. En bruger kan kun være deltager én gang.
- **Send:** kun aktive deltagere (`leftAt` er tom) kan sende.
- **Læs:** kun aktive deltagere kan læse chatten, dens beskeder og receipts (`CanRead`, i dag samme regel som `CanSend`).
- **Edit/Delete:** kun afsenderen, og kun mens de er aktiv deltager. En slettet besked kan ikke redigeres. Sletning er blød: `content` fjernes, og `deletedAt` sættes én gang (gentagen sletning er en no-op).
- **Receipt:** ingen receipt for afsenderen selv. `deliveredAt` og `seenAt` sættes første gang og overskrives aldrig.
- **ChatTitle:** trimmet, 1-200 tegn. **MessageContent:** ikke tom, maks. 4000 tegn.
