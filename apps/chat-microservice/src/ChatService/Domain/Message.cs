namespace ChatService.Domain;

/// <summary>Aggregate root for a message. References Chat by id.</summary>
public sealed class Message
{
    public Guid Id { get; }
    public Guid ChatId { get; }
    public Guid SenderUserId { get; }

    /// <summary>Null when the message is deleted - the content is not kept after deletion.</summary>
    public MessageContent? Content { get; private set; }
    public DateTimeOffset SentAt { get; }
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    private Message(Guid id, Guid chatId, Guid senderUserId, MessageContent? content, DateTimeOffset sentAt,
        DateTimeOffset? editedAt = null, DateTimeOffset? deletedAt = null)
    {
        Id = id;
        ChatId = chatId;
        SenderUserId = senderUserId;
        Content = content;
        SentAt = sentAt;
        EditedAt = editedAt;
        DeletedAt = deletedAt;
    }

    public static Message Send(Chat chat, Guid senderUserId, MessageContent content, DateTimeOffset now)
    {
        if (!chat.CanSend(senderUserId))
            throw new NotAllowedException("Only active participants can send messages.");

        return new Message(Guid.CreateVersion7(now), chat.Id, senderUserId, content, now);
    }

    /// <summary>Rehydration from persistence - does not run creation invariants.</summary>
    internal static Message Restore(Guid id, Guid chatId, Guid senderUserId, MessageContent? content, DateTimeOffset sentAt,
        DateTimeOffset? editedAt = null, DateTimeOffset? deletedAt = null) =>
        new(id, chatId, senderUserId, content, sentAt, editedAt, deletedAt);

    /// <summary>Sender only, and only while they are still an active participant. A deleted message cannot be edited.</summary>
    public void Edit(Chat chat, Guid requestedByUserId, MessageContent content, DateTimeOffset now)
    {
        EnsureSenderCanChange(chat, requestedByUserId, "edit");

        if (IsDeleted)
            throw new ConflictException("A deleted message cannot be edited.");

        Content = content;
        EditedAt = now;
    }

    /// <summary>
    /// Soft delete: the message stays in the history (so paging and receipts still work), but the content is removed.
    /// Idempotent - returns false if it was already deleted.
    /// </summary>
    public bool Delete(Chat chat, Guid requestedByUserId, DateTimeOffset now)
    {
        EnsureSenderCanChange(chat, requestedByUserId, "delete");

        if (IsDeleted)
            return false;

        Content = null;
        DeletedAt = now;
        return true;
    }

    private void EnsureSenderCanChange(Chat chat, Guid userId, string action)
    {
        if (chat.Id != ChatId)
            throw new DomainException("The message does not belong to this chat.");

        if (userId != SenderUserId || !chat.CanSend(userId))
            throw new NotAllowedException($"Only the sender, as an active participant, can {action} the message.");
    }
}
