using Bizcord.Logging;

namespace FileService.Application;

/// <summary>Abstraction over the message broker - same pattern as ChatService and RTC (each service has its own copy).</summary>
public interface IMessageClient
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);
    Task SubscribeAsync<T>(string subscriptionId, Func<T, Task> handler);
}

internal static class MessageClientExtensions
{
    /// <summary>
    /// At-most-once: the change is already saved, so a broker failure must not undo it.
    /// If a reply is lost, ChatService's timeout sends ReleaseQuota, which cleans up.
    /// </summary>
    public static async Task TryPublishAsync<T>(this IMessageClient client, T message, ILogger logger)
    {
        try
        {
            await client.PublishAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to publish message. The change is saved, but the message is lost.", new { MessageType = typeof(T).Name }, ex);
        }
    }
}
