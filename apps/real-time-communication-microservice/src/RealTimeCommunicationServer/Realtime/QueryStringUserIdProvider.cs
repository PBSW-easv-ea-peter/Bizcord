using Microsoft.AspNetCore.SignalR;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>
/// Who the connection belongs to: ?userId=... (equivalent to ChatService's X-User-Id - we trust it in the MVP).
/// Query string rather than header, because the browser's WebSocket API cannot set headers.
/// To be replaced by a token (week 45); then this is the only place that needs to change.
/// </summary>
public class QueryStringUserIdProvider : IUserIdProvider
{
    public const string QueryKey = "userId";

    public string? GetUserId(HubConnectionContext connection) =>
        Normalize(connection.GetHttpContext()?.Request.Query[QueryKey].FirstOrDefault());

    /// <summary>
    /// Canonical Guid form (lowercase, with hyphens) - or null if it is not a Guid.
    /// SignalR matches Clients.Users(...) on the exact string, so "ABC…" and "abc…" would be two users:
    /// presence (Guid) would report them online, but the push would never arrive.
    /// </summary>
    public static string? Normalize(string? userId) =>
        Guid.TryParse(userId, out var id) ? id.ToString() : null;
}
