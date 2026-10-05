using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

/// <summary>Husker push i stedet for at sende via SignalR.</summary>
public class FakeClientNotifier : IClientNotifier
{
    public List<(IReadOnlyList<Guid> UserIds, MessageReceived Message)> Pushes { get; } = [];

    public Task PushMessageAsync(IReadOnlyList<Guid> userIds, MessageReceived message, CancellationToken cancellationToken = default)
    {
        Pushes.Add((userIds, message));
        return Task.CompletedTask;
    }
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
