using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ChatService.Tests.Api;

/// <summary>Tokens like the gateway's faucet issues them, signed with the key ChatApiFactory gives the service.</summary>
internal static class TestTokens
{
    public const string SigningKey = "test-signing-key-for-chatservice-api-tests-0123456789";
    public const string Issuer = "http://api-gateway";
    public const string Audience = "internal-api";

    public static string Create(string? sub, string? role = "member", string signingKey = SigningKey)
    {
        var claims = new List<Claim>();
        if (sub is not null) claims.Add(new Claim("sub", sub));
        if (role is not null) claims.Add(new Claim("role", role));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)
        });
    }

    /// <summary>Authenticates the client as a member with this user id.</summary>
    public static HttpClient AsMember(this HttpClient client, Guid userId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Create(userId.ToString()));
        return client;
    }
}
