using Bizcord.Logging;

namespace ChatService.Application;

internal static class MessageClientExtensions
{
    /// <summary>
    /// At-most-once: the change is already saved, so a broker failure must not fail the request (no outbox yet).
    /// Does not use the request's CancellationToken - an aborted request must not lose an event for a saved change.
    /// </summary>
    public static async Task TryPublishAsync<T>(this IMessageClient client, T message, ILogger logger)
    {
        try
        {
            await client.PublishAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to publish event. The change is saved, but the event is lost.", new { EventType = typeof(T).Name }, ex);
        }
    }
}
