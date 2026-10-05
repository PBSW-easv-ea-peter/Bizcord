using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

public class PresenceTrackerTests
{
    private readonly PresenceTracker _presence = new();
    private readonly Guid _alice = Guid.NewGuid();

    [Fact]
    public void User_with_two_connections_stays_online_until_the_last_disconnects()
    {
        _presence.Connected(_alice);
        _presence.Connected(_alice);

        _presence.Disconnected(_alice);
        Assert.True(_presence.IsOnline(_alice));

        _presence.Disconnected(_alice);
        Assert.False(_presence.IsOnline(_alice));
    }

    [Fact]
    public void OnlineAmong_returns_only_connected_users_once()
    {
        var bob = Guid.NewGuid();
        _presence.Connected(_alice);

        Assert.Equal([_alice], _presence.OnlineAmong([_alice, bob, _alice]));
    }
}
