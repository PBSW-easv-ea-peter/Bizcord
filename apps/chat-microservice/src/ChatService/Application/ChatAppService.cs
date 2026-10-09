using Bizcord.Logging;
using ChatService.Contracts;
using ChatService.Domain;

namespace ChatService.Application;

/// <summary>Use cases for the Chat aggregate. Orchestrates only - the business rules live in the domain.</summary>
public sealed class ChatAppService(
    IChatRepository chats,
    IMessageClient messageClient,
    TimeProvider time,
    ILogger<ChatAppService> logger)
{
    /// <summary>Idempotent: if a direct chat between the two already exists, it is returned.</summary>
    public async Task<(Chat Chat, bool Created)> CreateDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        var existing = await chats.FindDirectAsync(userA, userB, cancellationToken);
        if (existing is not null)
            return (existing, false);

        var chat = Chat.CreateDirect(userA, userB, time.GetUtcNowInMicroseconds());
        try
        {
            await chats.AddAsync(chat, cancellationToken);
            logger.Information("Direct chat created.", new { ChatId = chat.Id });
            return (chat, true);
        }
        catch (ConflictException)
        {
            // Lost the race against a concurrent request - return the winner.
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
        logger.Information("Group chat created.", new { ChatId = chat.Id });
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
