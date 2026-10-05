namespace ChatService.Domain;

/// <summary>
/// The first time a user had a message delivered/saw it. Timestamps are never overwritten.
/// Identity: (MessageId, UserId).
/// </summary>
public sealed class MessageReceipt
{
    public Guid MessageId { get; }
    public Guid UserId { get; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? SeenAt { get; private set; }

    private MessageReceipt(Guid messageId, Guid userId, DateTimeOffset? deliveredAt = null, DateTimeOffset? seenAt = null)
    {
        MessageId = messageId;
        UserId = userId;
        DeliveredAt = deliveredAt;
        SeenAt = seenAt;
    }

    public static MessageReceipt Create(Message message, Guid userId)
    {
        if (message.SenderUserId == userId)
            throw new DomainException("A sender cannot have a receipt for their own message.");

        return new MessageReceipt(message.Id, userId);
    }

    /// <summary>Rehydration from persistence - does not run creation invariants.</summary>
    internal static MessageReceipt Restore(Guid messageId, Guid userId, DateTimeOffset? deliveredAt, DateTimeOffset? seenAt) =>
        new(messageId, userId, deliveredAt, seenAt);

    public void MarkDelivered(DateTimeOffset at) => DeliveredAt ??= at;

    public void MarkSeen(DateTimeOffset at) => SeenAt ??= at;
}
