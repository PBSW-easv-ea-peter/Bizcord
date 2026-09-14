using RealTimeCommunicationServer.Models;

namespace RealTimeCommunicationServer.Messaging;

public class HandleMessages
{
    private readonly IMessageClient _messageClient;
    private readonly ILogger<HandleMessages> _logger;

    public HandleMessages(IMessageClient messageClient, ILogger<HandleMessages> logger)
    {
        _messageClient = messageClient;
        _logger = logger;
    }

    public Task Subscribe()
    {
        return _messageClient.Subscribe<PingMessage>(
            "real-time-server",
            message =>
            {
                _logger.LogWarning("Received message: {message}", message.Text);
                return Task.CompletedTask;
            });
    }
}