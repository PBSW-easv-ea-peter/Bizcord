namespace Bizcord.ApiGateway.Application.Services;

public class JwtOptions
{
    public required string AuthenticationProviderKey { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int ExpiryHours { get; init; }
}