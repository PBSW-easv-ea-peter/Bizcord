namespace ChatService.Domain;

/// <summary>Aggregat-rod for en besked. Refererer Chat via id.</summary>
public sealed class Message
{
    public Guid Id { get; }
    public Guid ChatId { get; }
    public Guid SenderUserId { get; }

    /// <summary>Null, når beskeden er slettet - indholdet gemmes ikke efter sletning.</summary>
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

    /// <summary>Genopbygning fra persistens - kører ikke oprettelses-invarianter.</summary>
    internal static Message Restore(Guid id, Guid chatId, Guid senderUserId, MessageContent? content, DateTimeOffset sentAt,
        DateTimeOffset? editedAt = null, DateTimeOffset? deletedAt = null) =>
        new(id, chatId, senderUserId, content, sentAt, editedAt, deletedAt);

    /// <summary>Kun afsenderen, og kun mens de stadig er aktiv deltager. En slettet besked kan ikke redigeres.</summary>
    public void Edit(Chat chat, Guid requestedByUserId, MessageContent content, DateTimeOffset now)
    {
        EnsureSenderCanChange(chat, requestedByUserId, "edit");

        if (IsDeleted)
            throw new ConflictException("A deleted message cannot be edited.");

        Content = content;
        EditedAt = now;
    }

    /// <summary>
    /// Blød sletning: beskeden bliver i historikken (så paging og receipts stadig virker), men indholdet fjernes.
    /// Idempotent - returnerer false, hvis den allerede var slettet.
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
