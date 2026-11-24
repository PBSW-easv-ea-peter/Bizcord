using System.Net;
using System.Text.Json;

namespace ChatService.Infrastructure.Web;

/// <summary>
/// Reads the JWT signing key from Vault at startup. Own copy of the gateway's provider - services share no code.
/// ChatService has its own Vault token, so it can be revoked without touching the gateway.
/// </summary>
internal static class VaultSecretProvider
{
    public static async Task<string> GetJwtSigningKeyAsync(IConfiguration configuration)
    {
        var address = configuration["Vault:Address"] ?? "http://vault:8200";
        var token = configuration["Vault:Token"];
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Vault token is required. Set CHATSERVICE_VAULT_TOKEN in .env - see README.");

        using var client = new HttpClient { BaseAddress = new Uri(address.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("X-Vault-Token", token);

        using var response = await client.GetAsync("v1/kv/data/secrets");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Vault denied access: check token, TTL and policy.");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var key = document.RootElement.GetProperty("data").GetProperty("data").GetProperty("JwtSigningKey").GetString();

        return string.IsNullOrWhiteSpace(key)
            ? throw new InvalidOperationException("Vault secret JwtSigningKey is missing or empty.")
            : key;
    }
}
