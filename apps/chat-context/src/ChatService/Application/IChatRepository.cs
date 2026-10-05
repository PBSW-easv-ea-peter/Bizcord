using ChatService.Domain;

namespace ChatService.Application;

public interface IChatRepository
{
    Task<Chat?> GetAsync(Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>Finds an existing direct chat between the two users (order does not matter).</summary>
    Task<Chat?> FindDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);

    /// <summary>Saves the chat and its participants in a single transaction.</summary>
    Task AddAsync(Chat chat, CancellationToken cancellationToken = default);

    Task AddParticipantAsync(Guid chatId, ChatParticipant participant, CancellationToken cancellationToken = default);
}
