using Microsoft.AspNetCore.SignalR;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>
/// Abstraction over the push transport (same idea as IMessageClient), so handlers can be tested without SignalR.
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

    // Users(...) reaches every connection of the users - including multiple devices per user.
    public Task PushMessageAsync(
        IReadOnlyList<Guid> userIds,
        MessageReceived message,
        CancellationToken cancellationToken = default) =>
        _hub.Clients.Users(userIds.Select(id => id.ToString())).MessageReceived(message);
}
