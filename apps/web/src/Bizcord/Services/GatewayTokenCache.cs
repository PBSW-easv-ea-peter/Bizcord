using System.Collections.Concurrent;

namespace Bizcord.Services;

/// <summary>
/// One gateway token per test user, so every page load doesn't ask the gateway's faucet for a new one.
/// Singleton - GatewayClient itself is created per request.
/// </summary>
public sealed class GatewayTokenCache(TimeProvider time)
{
    // Well inside the gateway's own expiry (Jwt:ExpiryHours, 5 h), so a cached token is never sent expired.
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, (string Token, DateTimeOffset ExpiresAt)> _tokens = new();

    public bool TryGet(Guid userId, out string token)
    {
        if (_tokens.TryGetValue(userId, out var entry) && entry.ExpiresAt > time.GetUtcNow())
        {
            token = entry.Token;
            return true;
        }

        token = string.Empty;
        return false;
    }

    public void Set(Guid userId, string token) => _tokens[userId] = (token, time.GetUtcNow() + Lifetime);

    /// <summary>Called when the gateway rejects a token (e.g. after a gateway restart with a new signing key).</summary>
    public void Remove(Guid userId) => _tokens.TryRemove(userId, out _);
}
