using Bizcord.Logging;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Messaging.Handlers;

public class ParticipantAddedHandler : IMessageHandler<ParticipantAdded>
{
    private readonly ILogger<ParticipantAddedHandler> _logger;

    public ParticipantAddedHandler(
        ILogger<ParticipantAddedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        ParticipantAdded message,
        CancellationToken cancellationToken)
    {
        _logger.Information(
            "ParticipantAdded received.",
            new { message.ChatId, message.UserId, message.Role });

        return Task.CompletedTask;
    }
}
