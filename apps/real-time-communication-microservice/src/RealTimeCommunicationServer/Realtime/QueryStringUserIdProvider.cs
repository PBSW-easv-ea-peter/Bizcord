using Microsoft.AspNetCore.SignalR;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>
/// Hvem forbindelsen tilhører: ?userId=... (svarer til ChatService' X-User-Id - vi stoler på den i MVP).
/// Query frem for header, fordi browserens WebSocket-API ikke kan sætte headere.
/// Erstattes af et token (uge 45); så er det kun her, der skal ændres.
/// </summary>
public class QueryStringUserIdProvider : IUserIdProvider
{
    public const string QueryKey = "userId";

    public string? GetUserId(HubConnectionContext connection) =>
        Normalize(connection.GetHttpContext()?.Request.Query[QueryKey].FirstOrDefault());

    /// <summary>
    /// Kanonisk Guid-form (små bogstaver, med bindestreger) - eller null, hvis det ikke er en Guid.
    /// SignalR matcher Clients.Users(...) på den præcise streng, så "ABC…" og "abc…" ville være to brugere:
    /// presence (Guid) ville kalde dem online, men pushet ville aldrig nå frem.
    /// </summary>
    public static string? Normalize(string? userId) =>
        Guid.TryParse(userId, out var id) ? id.ToString() : null;
}
