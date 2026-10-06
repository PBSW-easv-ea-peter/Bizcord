using ChatService.Domain;

namespace ChatService.Application;

public interface IReceiptRepository
{
    /// <summary>
    /// Markerer alle andres beskeder i chatten til og med <paramref name="upTo"/> som set af brugeren.
    /// Allerede sete beskeder røres ikke. Returnerer antal nyligt markerede.
    /// </summary>
    Task<int> MarkSeenUpToAsync(Guid userId, Message upTo, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Markerer beskeden som leveret til brugerne. Kun aktive deltagere i chatten tæller; afsenderen og
    /// allerede leverede røres ikke, og en ukendt besked giver 0. Returnerer antal nyligt markerede.
    /// </summary>
    Task<int> MarkDeliveredAsync(Guid messageId, IReadOnlyCollection<Guid> userIds, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageReceipt>> GetForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}
