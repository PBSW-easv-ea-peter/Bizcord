using ChatService.Domain;

namespace ChatService.Application;

public interface IReceiptRepository
{
    /// <summary>
    /// Marks all other users' messages in the chat up to and including <paramref name="upTo"/> as seen by the user.
    /// Already seen messages are left untouched. Returns the number of newly marked.
    /// </summary>
    Task<int> MarkSeenUpToAsync(Guid userId, Message upTo, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the message as delivered to the users. Only active participants in the chat count; the sender and
    /// already delivered are left untouched, and an unknown message yields 0. Returns the number of newly marked.
    /// </summary>
    Task<int> MarkDeliveredAsync(Guid messageId, IReadOnlyCollection<Guid> userIds, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageReceipt>> GetForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}
