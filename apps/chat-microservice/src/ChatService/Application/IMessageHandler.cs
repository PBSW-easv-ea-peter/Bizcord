namespace ChatService.Application;

/// <summary>
/// Handles one message type from the broker. The saga orchestrator implements it once per reply it receives,
/// as in the week 46 assignment - so the orchestrator is "one handler that receives every message in the saga".
/// </summary>
public interface IMessageHandler<in T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken);
}
