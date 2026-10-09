using Bizcord.Logging;
using ChatService.Contracts;
using ChatService.Domain;

namespace ChatService.Application;

/// <summary>Use cases for messages and receipts. Orchestrates only - the business rules live in the domain.</summary>
public sealed class MessageAppService(
    IChatRepository chats,
    IMessageRepository messages,
    IReceiptRepository receipts,
    IMessageClient messageClient,
    TimeProvider time,
    ILogger<MessageAppService> logger)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public async Task<Message> SendAsync(Guid chatId, Guid senderUserId, string content, CancellationToken cancellationToken = default)
    {
        var chat = await GetChatAsync(chatId, cancellationToken);

        var messageContent = new MessageContent(content);
        var message = Message.Send(chat, senderUserId, messageContent, time.GetUtcNowInMicroseconds());
        await messages.AddAsync(message, cancellationToken);
        logger.Information("Message sent.", new { ChatId = chat.Id, MessageId = message.Id });

        // Fat event: the recipients are included, so RTC can push without calling back.
        await messageClient.TryPublishAsync(new MessageSent(
            message.Id, chat.Id, senderUserId, messageContent.Value, message.SentAt, Recipients(chat, senderUserId)), logger);

        return message;
    }

    public async Task<Message> EditAsync(Guid chatId, Guid requestedByUserId, Guid messageId, string content, CancellationToken cancellationToken = default)
    {
        var chat = await GetChatAsync(chatId, cancellationToken);
        var message = await GetMessageInChatAsync(chat.Id, messageId, cancellationToken);

        var messageContent = new MessageContent(content);
        message.Edit(chat, requestedByUserId, messageContent, time.GetUtcNowInMicroseconds());

        if (!await messages.UpdateAsync(message, cancellationToken))
            throw new ConflictException("A deleted message cannot be edited.");

        logger.Information("Message edited.", new { ChatId = chat.Id, MessageId = message.Id });

        await messageClient.TryPublishAsync(new MessageEdited(
            message.Id, chat.Id, messageContent.Value, message.EditedAt!.Value, Recipients(chat, message.SenderUserId)), logger);

        return message;
    }

    /// <summary>Idempotent: an already deleted message produces no new event.</summary>
    public async Task DeleteAsync(Guid chatId, Guid requestedByUserId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var chat = await GetChatAsync(chatId, cancellationToken);
        var message = await GetMessageInChatAsync(chat.Id, messageId, cancellationToken);

        if (!message.Delete(chat, requestedByUserId, time.GetUtcNowInMicroseconds()))
            return;

        // False means a concurrent request deleted it first - that request has published the event.
        if (!await messages.UpdateAsync(message, cancellationToken))
            return;

        logger.Information("Message deleted.", new { ChatId = chat.Id, MessageId = message.Id });

        await messageClient.TryPublishAsync(new MessageDeleted(
            message.Id, chat.Id, message.DeletedAt!.Value, Recipients(chat, message.SenderUserId)), logger);
    }

    public async Task<IReadOnlyList<Message>> GetPageAsync(
        Guid chatId, Guid requestedByUserId, Guid? beforeMessageId, int limit = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var chat = await GetReadableChatAsync(chatId, requestedByUserId, cancellationToken);

        var before = beforeMessageId is null
            ? null
            : await GetMessageInChatAsync(chat.Id, beforeMessageId.Value, cancellationToken);

        return await messages.GetPageAsync(chat.Id, before, Math.Clamp(limit, 1, MaxPageSize), cancellationToken);
    }

    public async Task MarkSeenUpToAsync(Guid chatId, Guid userId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var chat = await GetReadableChatAsync(chatId, userId, cancellationToken);

        // The cursor is read from the DB, so sent_at matches exactly in the "up to and including" comparison.
        var upTo = await GetMessageInChatAsync(chat.Id, messageId, cancellationToken);

        var now = time.GetUtcNowInMicroseconds();
        var marked = await receipts.MarkSeenUpToAsync(userId, upTo, now, cancellationToken);

        logger.Information("Messages marked as seen.", new { ChatId = chat.Id, UpToMessageId = upTo.Id, Marked = marked });

        // Only when something actually changed - repeated calls don't create noise on the bus.
        if (marked > 0)
            await messageClient.TryPublishAsync(new MessagesSeen(chat.Id, userId, upTo.Id, now), logger);
    }

    /// <summary>
    /// From RTC's 'rtc.message-delivered'. No access check - it is an internal notification, not a user call.
    /// An unknown message is ignored: the REST API is the source of truth, events are notifications.
    /// </summary>
    public async Task MarkDeliveredAsync(Guid messageId, IReadOnlyCollection<Guid> userIds, DateTimeOffset deliveredAt, CancellationToken cancellationToken = default)
    {
        var marked = await receipts.MarkDeliveredAsync(messageId, userIds, deliveredAt, cancellationToken);

        logger.Information("Message marked as delivered.", new { MessageId = messageId, Recipients = userIds.Count, Marked = marked });
    }

    public async Task<IReadOnlyList<MessageReceipt>> GetReceiptsAsync(
        Guid chatId, Guid requestedByUserId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var chat = await GetReadableChatAsync(chatId, requestedByUserId, cancellationToken);
        var message = await GetMessageInChatAsync(chat.Id, messageId, cancellationToken);

        return await receipts.GetForMessageAsync(message.Id, cancellationToken);
    }

    private static List<Guid> Recipients(Chat chat, Guid senderUserId) =>
        chat.Participants
            .Where(p => p.IsActive && p.UserId != senderUserId)
            .Select(p => p.UserId)
            .ToList();

    private async Task<Chat> GetChatAsync(Guid chatId, CancellationToken cancellationToken) =>
        await chats.GetAsync(chatId, cancellationToken)
        ?? throw new NotFoundException($"Chat '{chatId}' was not found.");

    private async Task<Chat> GetReadableChatAsync(Guid chatId, Guid userId, CancellationToken cancellationToken)
    {
        var chat = await GetChatAsync(chatId, cancellationToken);

        if (!chat.CanRead(userId))
            throw new NotAllowedException("Only active participants can read the chat.");

        return chat;
    }

    /// <summary>A message from another chat is treated as not found - it does not belong to this resource.</summary>
    private async Task<Message> GetMessageInChatAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await messages.GetAsync(messageId, cancellationToken);

        return message is not null && message.ChatId == chatId
            ? message
            : throw new NotFoundException($"Message '{messageId}' was not found in chat '{chatId}'.");
    }
}
