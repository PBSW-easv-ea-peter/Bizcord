using Bizcord.Logging;

namespace ChatService.Application;

internal static class MessageClientExtensions
{
    /// <summary>
    /// At-most-once: ændringen er allerede gemt, så en broker-fejl må ikke fejle requesten (ingen outbox endnu).
    /// Bruger ikke requestens CancellationToken - en afbrudt request må ikke tabe et event for en gemt ændring.
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
