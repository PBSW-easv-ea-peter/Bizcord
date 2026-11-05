// Dev-only token issuer: issues any token to anyone, without login.
// Follows docs/documentation/token-contract.md, so validators run the same code against it as against UserAuth in production.

using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var issuer = app.Configuration["Issuer"]
    ?? throw new InvalidOperationException("Issuer is required.");
var lifetime = TimeSpan.FromMinutes(app.Configuration.GetValue("LifetimeMinutes", 15));

// The audience follows from the role, so a caller cannot request a combination the contract does not allow.
var audiences = new Dictionary<string, string>
{
    ["member"] = "bizcord",
    ["internal-service"] = "bizcord-internal",
};

// A new key on every start: nothing secret on disk or in the repo, and a restart acts as a key rotation.
var rsa = RSA.Create(2048);
var keyId = Guid.NewGuid().ToString("N");
var signingCredentials = new SigningCredentials(
    new RsaSecurityKey(rsa) { KeyId = keyId }, SecurityAlgorithms.RsaSha256);
var publicKey = rsa.ExportParameters(includePrivateParameters: false);
var tokenHandler = new JsonWebTokenHandler();

app.Logger.LogWarning("Dev-only token issuer started (kid {KeyId}). Never run this in production.", keyId);

app.MapPost("/token", (TokenRequest request) =>
{
    if (request.Role is null || !audiences.TryGetValue(request.Role, out var audience))
        return Results.Problem($"role must be one of: {string.Join(", ", audiences.Keys)}.", statusCode: 400);

    var sub = request.Sub?.Trim();
    if (string.IsNullOrEmpty(sub))
        return Results.Problem("sub is required.", statusCode: 400);

    // ChatService and RTC use sub as a Guid user id - fail here rather than with a confusing 401/400 downstream.
    if (request.Role == "member")
    {
        if (!Guid.TryParse(sub, out var userId))
            return Results.Problem("sub must be a Guid for role member.", statusCode: 400);
        sub = userId.ToString();
    }

    var now = DateTime.UtcNow;
    var token = tokenHandler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = issuer,
        Audience = audience,
        IssuedAt = now,
        NotBefore = now,
        Expires = now + lifetime,
        Claims = new Dictionary<string, object> { ["sub"] = sub, ["role"] = request.Role },
        SigningCredentials = signingCredentials,
    });

    return Results.Ok(new
    {
        access_token = token,
        token_type = "Bearer",
        expires_in = (int)lifetime.TotalSeconds,
    });
});

app.MapGet("/.well-known/openid-configuration", (HttpRequest request) =>
{
    // Built from the caller's host: the browser, the gateway and the containers reach the issuer under different names.
    var baseUrl = $"{request.Scheme}://{request.Host}";
    return Results.Ok(new
    {
        issuer,
        jwks_uri = $"{baseUrl}/.well-known/jwks.json",
        token_endpoint = $"{baseUrl}/token",
        id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 },
    });
});

// Only the public part of the key (modulus and exponent).
app.MapGet("/.well-known/jwks.json", () => Results.Ok(new
{
    keys = new[]
    {
        new
        {
            kty = "RSA",
            use = "sig",
            alg = SecurityAlgorithms.RsaSha256,
            kid = keyId,
            n = Base64UrlEncoder.Encode(publicKey.Modulus),
            e = Base64UrlEncoder.Encode(publicKey.Exponent),
        },
    },
}));

app.Run();

internal record TokenRequest(string? Sub, string? Role);
