using ChatService.Domain;

namespace ChatService.Application;

public interface IChatRepository
{
    Task<Chat?> GetAsync(Guid chatId, CancellationToken cancellationToken = default);

    /// <summary>Finder en eksisterende direct-chat mellem de to brugere (rækkefølgen er ligegyldig).</summary>
    Task<Chat?> FindDirectAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);

    /// <summary>Gemmer chatten og dens deltagere i én transaktion.</summary>
    Task AddAsync(Chat chat, CancellationToken cancellationToken = default);

    Task AddParticipantAsync(Guid chatId, ChatParticipant participant, CancellationToken cancellationToken = default);
}
