using Bizcord.Logging;
using RealTimeCommunicationServer.Models;

namespace RealTimeCommunicationServer.Messaging.Handlers;

public class PingMessageHandler : IMessageHandler<PingMessage>
{
    private readonly ILogger<PingMessageHandler> _logger;

    public PingMessageHandler(
        ILogger<PingMessageHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        PingMessage message,
        CancellationToken cancellationToken)
    {
        _logger.Warning(
            "PingMessage received.",
            new { message.Text });

        return Task.CompletedTask;
    }
}
