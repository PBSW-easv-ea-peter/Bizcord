namespace FileService.Application;

/// <summary>Handles one message type from the broker - FileService's own copy, like IMessageClient (no shared code).</summary>
public interface IMessageHandler<in T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken);
}
