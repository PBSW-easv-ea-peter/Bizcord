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
    public const string MessageEdited = "chat.message-edited";
    public const string MessageDeleted = "chat.message-deleted";

    /// <summary>Consumes - published by Real-Time Communication Service.</summary>
    public const string MessageDelivered = "rtc.message-delivered";

    // File-message saga. FileService owns these names: commands are addressed to it, events come from it.
    // See apps/file-microservice/docs/contracts.md.

    /// <summary>Sends (command).</summary>
    public const string ReserveQuota = "file.reserve-quota";

    /// <summary>Sends (command, compensation).</summary>
    public const string ReleaseQuota = "file.release-quota";

    /// <summary>Consumes.</summary>
    public const string QuotaReserved = "file.quota-reserved";

    /// <summary>Consumes.</summary>
    public const string QuotaRejected = "file.quota-rejected";

    /// <summary>Consumes.</summary>
    public const string QuotaReleased = "file.quota-released";
}

/// <summary>Content is null when the message only has a file. Attachment is null for plain text messages.</summary>
public sealed record MessageSent(
    Guid MessageId,
    Guid ChatId,
    Guid SenderUserId,
    string? Content,
    DateTimeOffset SentAt,
    IReadOnlyList<Guid> RecipientUserIds,
    AttachmentInfo? Attachment = null);

public sealed record AttachmentInfo(
    Guid FileId,
    string FileName,
    long SizeBytes);

public sealed record ParticipantAdded(
    Guid ChatId,
    Guid UserId,
    string Role,
    Guid AddedByUserId,
    DateTimeOffset JoinedAt);

/// <summary>Same recipients as MessageSent, so RTC can update the clients without calling back.</summary>
public sealed record MessageEdited(
    Guid MessageId,
    Guid ChatId,
    string Content,
    DateTimeOffset EditedAt,
    IReadOnlyList<Guid> RecipientUserIds);

/// <summary>Without content - it has just been deleted.</summary>
public sealed record MessageDeleted(
    Guid MessageId,
    Guid ChatId,
    DateTimeOffset DeletedAt,
    IReadOnlyList<Guid> RecipientUserIds);

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
