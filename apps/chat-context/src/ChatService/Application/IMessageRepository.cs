using ChatService.Domain;

namespace ChatService.Application;

public interface IMessageRepository
{
    Task<Message?> GetAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task AddAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>Nyeste først. Med <paramref name="before"/> hentes beskeder sendt før den.</summary>
    Task<IReadOnlyList<Message>> GetPageAsync(Guid chatId, Message? before, int limit, CancellationToken cancellationToken = default);
}
