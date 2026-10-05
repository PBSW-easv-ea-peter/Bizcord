namespace ChatService.Contracts;

/// <summary>
/// Sprogneutrale event-navne - de er kontrakten, ikke C#-typenavnene.
/// Bliver både exchange-navn og 'type'-header i RabbitMQ. Se docs/contracts.md.
/// </summary>
public static class EventNames
{
    public const string MessageSent = "chat.message-sent";
    public const string ParticipantAdded = "chat.participant-added";
    public const string MessagesSeen = "chat.messages-seen";

    /// <summary>Consumes - publiceres af Real-Time Communication Service.</summary>
    public const string MessageDelivered = "rtc.message-delivered";
}

public sealed record MessageSent(
    Guid MessageId,
    Guid ChatId,
    Guid SenderUserId,
    string Content,
    DateTimeOffset SentAt,
    IReadOnlyList<Guid> RecipientUserIds);

public sealed record ParticipantAdded(
    Guid ChatId,
    Guid UserId,
    string Role,
    Guid AddedByUserId,
    DateTimeOffset JoinedAt);

/// <summary>Én pr. "til og med"-batch: alle andres beskeder til og med UpToMessageId er nu set af UserId.</summary>
public sealed record MessagesSeen(
    Guid ChatId,
    Guid UserId,
    Guid UpToMessageId,
    DateTimeOffset SeenAt);

/// <summary>
/// ChatService' egen læsning af RTC's 'rtc.message-delivered' - kun de felter, vi bruger (tolerant reader).
/// RTC har pushet beskeden til disse brugeres forbundne klienter.
/// </summary>
public sealed record MessageDelivered(
    Guid MessageId,
    IReadOnlyList<Guid> DeliveredToUserIds,
    DateTimeOffset DeliveredAt);
