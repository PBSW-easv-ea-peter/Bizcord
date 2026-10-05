using Microsoft.AspNetCore.SignalR;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>
/// Abstraktion over push-transporten (samme idé som IMessageClient), så handlers kan testes uden SignalR.
/// </summary>
public interface IClientNotifier
{
    Task PushMessageAsync(
        IReadOnlyList<Guid> userIds,
        MessageReceived message,
        CancellationToken cancellationToken = default);
}

public class SignalRClientNotifier : IClientNotifier
{
    private readonly IHubContext<ChatHub, IChatClient> _hub;

    public SignalRClientNotifier(
        IHubContext<ChatHub, IChatClient> hub)
    {
        _hub = hub;
    }

    // Users(...) rammer alle forbindelser for brugerne - også flere enheder pr. bruger.
    public Task PushMessageAsync(
        IReadOnlyList<Guid> userIds,
        MessageReceived message,
        CancellationToken cancellationToken = default) =>
        _hub.Clients.Users(userIds.Select(id => id.ToString())).MessageReceived(message);
}
