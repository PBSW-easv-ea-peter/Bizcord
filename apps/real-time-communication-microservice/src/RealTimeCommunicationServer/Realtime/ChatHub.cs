using Bizcord.Logging;
using Microsoft.AspNetCore.SignalR;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>Det klienten kan modtage. Metodenavnet er kontrakten (se docs/contracts.md).</summary>
public interface IChatClient
{
    Task MessageReceived(MessageReceived message);
}

/// <summary>
/// Klienter forbinder på /hubs/chat?userId=... og modtager push. Klienten kalder ikke noget på hubben -
/// den sender beskeder via ChatService' REST API.
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
        // Afviste forbindelser blev aldrig talt med.
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            _presence.Disconnected(userId);
            _logger.Information("Client disconnected.", new { UserId = userId, Context.ConnectionId });
        }

        await base.OnDisconnectedAsync(exception);
    }
}
