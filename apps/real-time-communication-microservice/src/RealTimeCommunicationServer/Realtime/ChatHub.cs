using Bizcord.Logging;
using Microsoft.AspNetCore.SignalR;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>What the client can receive. The method name is the contract (see docs/contracts.md).</summary>
public interface IChatClient
{
    Task MessageReceived(MessageReceived message);
}

/// <summary>
/// Clients connect to /hubs/chat?userId=... and receive push. The client calls nothing on the hub -
/// it sends messages via ChatService's REST API.
/// </summary>
public class ChatHub : Hub<IChatClient>
{
    public const string Path = "/hubs/chat";

    private readonly PresenceTracker _presence;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        PresenceTracker presence,
        ILogger<ChatHub> logger)
    {
        _presence = presence;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            _logger.Warning("Connection rejected: missing or invalid userId.", new { Context.ConnectionId });
            Context.Abort();
            return;
        }

        _presence.Connected(userId);
        _logger.Information("Client connected.", new { UserId = userId, Context.ConnectionId });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Rejected connections were never counted.
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            _presence.Disconnected(userId);
            _logger.Information("Client disconnected.", new { UserId = userId, Context.ConnectionId });
        }

        await base.OnDisconnectedAsync(exception);
    }
}
