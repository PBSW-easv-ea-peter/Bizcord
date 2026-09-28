using ChatService.Contracts;
using ChatService.Domain;

namespace ChatService.Application;

/// <summary>Use cases for Chat-aggregatet. Orkestrerer - forretningsreglerne ligger i domænet.</summary>
public sealed class ChatAppService(
    IChatRepository chats,
    IMessageClient messageClient,
    TimeProvider time,
    ILogger<ChatAppService> logger)
{
    /// <summary>Idempotent: findes der allerede en direct-chat mellem de to, returneres den.</summary>
    public async Task<(Chat Chat, bool Created)> CreateDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        var existing = await chats.FindDirectAsync(userA, userB, cancellationToken);
        if (existing is not null)
            return (existing, false);

        var chat = Chat.CreateDirect(userA, userB, time.GetUtcNowInMicroseconds());
        try
        {
            await chats.AddAsync(chat, cancellationToken);
            return (chat, true);
        }
        catch (ConflictException)
        {
            // Tabte kapløbet mod en samtidig request - returnér vinderen.
            var winner = await chats.FindDirectAsync(userA, userB, cancellationToken);
            return winner is not null ? (winner, false) : throw new InvalidOperationException("Direct chat conflict without an existing chat.");
        }
    }

    public async Task<Chat> GetAsync(Guid chatId, Guid requestedByUserId, CancellationToken cancellationToken = default)
    {
        var chat = await chats.GetAsync(chatId, cancellationToken)
            ?? throw new NotFoundException($"Chat '{chatId}' was not found.");

        if (!chat.CanRead(requestedByUserId))
            throw new NotAllowedException("Only active participants can read the chat.");

        return chat;
    }

    public async Task<Chat> CreateGroupAsync(Guid creatorUserId, string title, CancellationToken cancellationToken = default)
    {
        var chat = Chat.CreateGroup(creatorUserId, new ChatTitle(title), time.GetUtcNowInMicroseconds());
        await chats.AddAsync(chat, cancellationToken);
        return chat;
    }

    public async Task<ChatParticipant> AddParticipantAsync(Guid chatId, Guid requestedByUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var chat = await chats.GetAsync(chatId, cancellationToken)
            ?? throw new NotFoundException($"Chat '{chatId}' was not found.");

        var participant = chat.AddParticipant(requestedByUserId, userId, time.GetUtcNowInMicroseconds());
        await chats.AddParticipantAsync(chatId, participant, cancellationToken);

        await messageClient.TryPublishAsync(new ParticipantAdded(
            chat.Id, participant.UserId, participant.Role.ToString(), requestedByUserId, participant.JoinedAt), logger);

        return participant;
    }
}
