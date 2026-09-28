using ChatService.Domain;

namespace ChatService.Application;

public interface IReceiptRepository
{
    /// <summary>
    /// Markerer alle andres beskeder i chatten til og med <paramref name="upTo"/> som set af brugeren.
    /// Allerede sete beskeder røres ikke. Returnerer antal nyligt markerede.
    /// </summary>
    Task<int> MarkSeenUpToAsync(Guid userId, Message upTo, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageReceipt>> GetForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}
