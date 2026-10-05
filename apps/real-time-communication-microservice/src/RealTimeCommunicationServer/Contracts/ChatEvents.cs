namespace RealTimeCommunicationServer.Contracts;

// RTC's own reading of ChatService's events - only the fields RTC uses (tolerant reader).
// The contract is the event name and the JSON shape, not a shared class. See apps/chat-context/docs/contracts.md.

/// <summary>Content is included because RTC has to push the message to the recipients. It must never be logged.</summary>
public record MessageSent(
    Guid MessageId,
    Guid ChatId,
    Guid SenderUserId,
    string Content,
    DateTimeOffset SentAt,
    IReadOnlyList<Guid> RecipientUserIds);

public record ParticipantAdded(
    Guid ChatId,
    Guid UserId,
    string Role,
    DateTimeOffset JoinedAt);

public record MessagesSeen(
    Guid ChatId,
    Guid UserId,
    Guid UpToMessageId,
    DateTimeOffset SeenAt);
