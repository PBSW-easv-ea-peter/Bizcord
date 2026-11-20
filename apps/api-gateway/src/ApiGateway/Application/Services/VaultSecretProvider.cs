using System.Net;
using System.Text.Json;

namespace Bizcord.ApiGateway.Application.Services;

public class VaultSecretProvider
{
    private readonly string _vaultAddr;
    private readonly string _vaultToken;
    private readonly HttpClient _httpClient;

    public VaultSecretProvider(IConfiguration configuration)
    {
        _vaultAddr = configuration["Vault:Address"] ?? "http://vault:8200";
        _vaultToken = configuration["Vault:Token"]
            ?? throw new InvalidOperationException("Vault token is required. Set VAULT_TOKEN environment variable.");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_vaultAddr.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        _httpClient.DefaultRequestHeaders.Add("X-Vault-Token", _vaultToken);
    }

    public async Task<string> GetJwtSigningKeyAsync()
    {
        using var response = await _httpClient.GetAsync("v1/kv/data/secrets");

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Vault denied access: check token, TTL and policy.");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var jwtSigningKey = document.RootElement
            .GetProperty("data")
            .GetProperty("data")
            .GetProperty("JwtSigningKey")
            .GetString();

        if (string.IsNullOrWhiteSpace(jwtSigningKey))
            throw new InvalidOperationException("Vault secret JwtSigningKey is missing or empty.");

        return jwtSigningKey;
    }
}
