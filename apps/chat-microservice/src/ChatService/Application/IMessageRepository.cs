using ChatService.Domain;

namespace ChatService.Application;

public interface IMessageRepository
{
    Task<Message?> GetAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task AddAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves content, editedAt and deletedAt. Does not touch an already deleted message, so a concurrent delete wins.
    /// Returns false if nothing was saved.
    /// </summary>
    Task<bool> UpdateAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>Newest first. With <paramref name="before"/>, fetches messages sent before that one.</summary>
    Task<IReadOnlyList<Message>> GetPageAsync(Guid chatId, Message? before, int limit, CancellationToken cancellationToken = default);
}
