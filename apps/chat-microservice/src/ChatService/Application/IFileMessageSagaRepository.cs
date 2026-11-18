using ChatService.Domain;

namespace ChatService.Application;

/// <summary>
/// Persists the saga state between messages - the handlers are stateless.
/// Updates are conditional on the status the saga had when it was loaded (optimistic concurrency):
/// a reply from FileService and the timeout job can race for the same saga, and only one of them may win.
/// </summary>
public interface IFileMessageSagaRepository
{
    Task<FileMessageSaga?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default);

    Task AddAsync(FileMessageSaga saga, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the saga no longer has <paramref name="expectedStatus"/> - someone else got there first.</summary>
    Task<bool> UpdateAsync(FileMessageSaga saga, FileMessageSagaStatus expectedStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the message and the saga update in one local transaction - step 3 of the saga.
    /// Returns false (and saves nothing) if the saga no longer has <paramref name="expectedStatus"/>.
    /// </summary>
    Task<bool> CompleteWithMessageAsync(FileMessageSaga saga, FileMessageSagaStatus expectedStatus, Message message, CancellationToken cancellationToken = default);

    /// <summary>Sagas in AwaitingQuota or Compensating that have not changed since <paramref name="notUpdatedSince"/>.</summary>
    Task<IReadOnlyList<FileMessageSaga>> GetStaleAsync(DateTimeOffset notUpdatedSince, int limit, CancellationToken cancellationToken = default);
}
