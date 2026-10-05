namespace ChatService.Domain;

/// <summary>Aggregate root for a message. References Chat by id.</summary>
public sealed class Message
{
    public Guid Id { get; }
    public Guid ChatId { get; }
    public Guid SenderUserId { get; }
    public MessageContent Content { get; }
    public DateTimeOffset SentAt { get; }

    private Message(Guid id, Guid chatId, Guid senderUserId, MessageContent content, DateTimeOffset sentAt)
    {
        Id = id;
        ChatId = chatId;
        SenderUserId = senderUserId;
        Content = content;
        SentAt = sentAt;
    }

    public static Message Send(Chat chat, Guid senderUserId, MessageContent content, DateTimeOffset now)
    {
        if (!chat.CanSend(senderUserId))
            throw new NotAllowedException("Only active participants can send messages.");

        return new Message(Guid.CreateVersion7(now), chat.Id, senderUserId, content, now);
    }

    /// <summary>Rehydration from persistence - does not run creation invariants.</summary>
    internal static Message Restore(Guid id, Guid chatId, Guid senderUserId, MessageContent content, DateTimeOffset sentAt) =>
        new(id, chatId, senderUserId, content, sentAt);
}
