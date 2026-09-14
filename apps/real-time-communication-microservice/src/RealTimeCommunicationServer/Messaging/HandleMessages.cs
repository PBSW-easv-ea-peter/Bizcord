using RealTimeCommunicationServer.Models;

namespace RealTimeCommunicationServer.Messaging;

public class HandleMessages : BackgroundService
{
    private readonly IMessageClient _messageClient;
    private readonly ILogger<HandleMessages> _logger;

    public HandleMessages(
        IMessageClient messageClient,
        ILogger<HandleMessages> logger)
    {
        _messageClient = messageClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await _messageClient.Subscribe<PingMessage>(
            "real-time-server",
            message =>
            {
                _logger.LogWarning(
                    "Received message: {Message}",
                    message.Text);

                return Task.CompletedTask;
            });
    }
}