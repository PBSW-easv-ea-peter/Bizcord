namespace RealTimeCommunicationServer.Contracts;

// RTC's egen læsning af ChatService's events - kun de felter, RTC bruger (tolerant reader).
// Kontrakten er event-navnet og JSON-formen, ikke en delt klasse. Se apps/chat-context/docs/contracts.md.

/// <summary>Content er med, fordi RTC skal pushe beskeden til modtagerne. Den må aldrig logges.</summary>
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
