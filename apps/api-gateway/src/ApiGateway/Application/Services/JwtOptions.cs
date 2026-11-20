namespace Bizcord.ApiGateway.Application.Services;

public class JwtOptions
{
    public string? AuthenticationProviderKey { get; set; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int ExpiryHours { get; init; }
}