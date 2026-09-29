using Bizcord.Logging;
using ChatService.Contracts;
using ChatService.Domain;

namespace ChatService.Application;

/// <summary>Use cases for beskeder og receipts. Orkestrerer - forretningsreglerne ligger i domænet.</summary>
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

        var message = Message.Send(chat, senderUserId, new MessageContent(content), time.GetUtcNowInMicroseconds());
        await messages.AddAsync(message, cancellationToken);
        logger.Information("Message sent.", new { ChatId = chat.Id, MessageId = message.Id });

        // Fedt event: modtagerne er med, så RTC kan pushe uden at kalde tilbage.
        var recipients = chat.Participants
            .Where(p => p.IsActive && p.UserId != senderUserId)
            .Select(p => p.UserId)
            .ToList();
        await messageClient.TryPublishAsync(new MessageSent(
            message.Id, chat.Id, senderUserId, message.Content.Value, message.SentAt, recipients), logger);

        return message;
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

        // Cursoren hentes fra DB'en, så sent_at matcher præcist i "til og med"-sammenligningen.
        var upTo = await GetMessageInChatAsync(chat.Id, messageId, cancellationToken);

        var now = time.GetUtcNowInMicroseconds();
        var marked = await receipts.MarkSeenUpToAsync(userId, upTo, now, cancellationToken);

        logger.Information("Messages marked as seen.", new { ChatId = chat.Id, UpToMessageId = upTo.Id, Marked = marked });

        // Kun når noget faktisk ændrede sig - gentagne kald giver ikke støj på bussen.
        if (marked > 0)
            await messageClient.TryPublishAsync(new MessagesSeen(chat.Id, userId, upTo.Id, now), logger);
    }

    public async Task<IReadOnlyList<MessageReceipt>> GetReceiptsAsync(
        Guid chatId, Guid requestedByUserId, Guid messageId, CancellationToken cancellationToken = default)
    {
        var chat = await GetReadableChatAsync(chatId, requestedByUserId, cancellationToken);
        var message = await GetMessageInChatAsync(chat.Id, messageId, cancellationToken);

        return await receipts.GetForMessageAsync(message.Id, cancellationToken);
    }

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

    /// <summary>En besked fra en anden chat behandles som ikke-fundet - den hører ikke til denne ressource.</summary>
    private async Task<Message> GetMessageInChatAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await messages.GetAsync(messageId, cancellationToken);

        return message is not null && message.ChatId == chatId
            ? message
            : throw new NotFoundException($"Message '{messageId}' was not found in chat '{chatId}'.");
    }
}
