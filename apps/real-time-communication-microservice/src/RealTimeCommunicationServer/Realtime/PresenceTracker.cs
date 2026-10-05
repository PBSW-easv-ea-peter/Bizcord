using System.Collections.Concurrent;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>
/// Hvem er forbundet lige nu. Tæller forbindelser pr. bruger, så en bruger med to enheder
/// først er offline, når den sidste lukker. Kun i hukommelsen - én instans (backplane er parkeret).
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    public void Connected(Guid userId) =>
        _connections.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void Disconnected(Guid userId)
    {
        var remaining = _connections.AddOrUpdate(userId, 0, (_, count) => count - 1);

        // Kun fjern, hvis tælleren stadig er 0 - en ny forbindelse kan være kommet imellem.
        if (remaining <= 0)
            _connections.TryRemove(new KeyValuePair<Guid, int>(userId, remaining));
    }

    public bool IsOnline(Guid userId) => _connections.ContainsKey(userId);

    public IReadOnlyList<Guid> OnlineAmong(IEnumerable<Guid> userIds) =>
        userIds.Where(IsOnline).Distinct().ToList();
}
