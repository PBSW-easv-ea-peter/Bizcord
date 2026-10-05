using System.Collections.Concurrent;

namespace RealTimeCommunicationServer.Realtime;

/// <summary>
/// Who is connected right now. Counts connections per user, so a user with two devices
/// is only offline once the last one closes. In-memory only - single instance (backplane is parked).
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    public void Connected(Guid userId) =>
        _connections.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void Disconnected(Guid userId)
    {
        var remaining = _connections.AddOrUpdate(userId, 0, (_, count) => count - 1);

        // Only remove if the counter is still 0 - a new connection may have arrived in between.
        if (remaining <= 0)
            _connections.TryRemove(new KeyValuePair<Guid, int>(userId, remaining));
    }

    public bool IsOnline(Guid userId) => _connections.ContainsKey(userId);

    public IReadOnlyList<Guid> OnlineAmong(IEnumerable<Guid> userIds) =>
        userIds.Where(IsOnline).Distinct().ToList();
}
