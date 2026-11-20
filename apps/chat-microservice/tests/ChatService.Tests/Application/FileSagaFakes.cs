using ChatService.Application;
using ChatService.Domain;

namespace ChatService.Tests.Application;

/// <summary>
/// Keeps the saved status per saga next to the object, so the conditional update (expected status)
/// behaves like the SQL "where status = @ExpectedStatus".
/// </summary>
internal sealed class InMemoryFileMessageSagaRepository : IFileMessageSagaRepository
{
    private readonly Dictionary<Guid, (FileMessageSaga Saga, FileMessageSagaStatus SavedStatus)> _sagas = [];

    public List<Message> SavedMessages { get; } = [];

    /// <summary>Simulates the timeout job (or a reply) moving the saga on just before our update.</summary>
    public bool LoseNextUpdate { get; set; }

    public FileMessageSagaStatus SavedStatus(Guid sagaId) => _sagas[sagaId].SavedStatus;

    public Task<FileMessageSaga?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sagas.TryGetValue(sagaId, out var entry) ? entry.Saga : null);

    public Task AddAsync(FileMessageSaga saga, CancellationToken cancellationToken = default)
    {
        _sagas.Add(saga.SagaId, (saga, saga.Status));
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(FileMessageSaga saga, FileMessageSagaStatus expectedStatus, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryUpdate(saga, expectedStatus));

    public Task<bool> CompleteWithMessageAsync(FileMessageSaga saga, FileMessageSagaStatus expectedStatus, Message message, CancellationToken cancellationToken = default)
    {
        if (!TryUpdate(saga, expectedStatus))
            return Task.FromResult(false);

        SavedMessages.Add(message);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<FileMessageSaga>> GetStaleAsync(DateTimeOffset notUpdatedSince, int limit, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FileMessageSaga> stale = _sagas.Values
            .Where(e => e.SavedStatus is FileMessageSagaStatus.AwaitingQuota or FileMessageSagaStatus.Compensating)
            .Select(e => e.Saga)
            .Where(s => s.UpdatedAt < notUpdatedSince)
            .Take(limit)
            .ToList();
        return Task.FromResult(stale);
    }

    private bool TryUpdate(FileMessageSaga saga, FileMessageSagaStatus expectedStatus)
    {
        if (LoseNextUpdate)
        {
            LoseNextUpdate = false;
            return false;
        }

        if (_sagas[saga.SagaId].SavedStatus != expectedStatus)
            return false;

        _sagas[saga.SagaId] = (saga, saga.Status);
        return true;
    }
}

internal sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
