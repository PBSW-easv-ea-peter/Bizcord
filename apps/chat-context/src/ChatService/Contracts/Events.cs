namespace ChatService.Contracts;

/// <summary>
/// Language-neutral event names - they are the contract, not the C# type names.
/// Used as both the exchange name and the 'type' header in RabbitMQ. See docs/contracts.md.
/// </summary>
public static class EventNames
{
    public const string MessageSent = "chat.message-sent";
    public const string ParticipantAdded = "chat.participant-added";
    public const string MessagesSeen = "chat.messages-seen";

    /// <summary>Consumes - published by Real-Time Communication Service.</summary>
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

/// <summary>One per "up to and including" batch: all other users' messages up to and including UpToMessageId are now seen by UserId.</summary>
public sealed record MessagesSeen(
    Guid ChatId,
    Guid UserId,
    Guid UpToMessageId,
    DateTimeOffset SeenAt);

/// <summary>
/// ChatService's own reading of RTC's 'rtc.message-delivered' - only the fields we use (tolerant reader).
/// RTC has pushed the message to these users' connected clients.
/// </summary>
public sealed record MessageDelivered(
    Guid MessageId,
    IReadOnlyList<Guid> DeliveredToUserIds,
    DateTimeOffset DeliveredAt);
