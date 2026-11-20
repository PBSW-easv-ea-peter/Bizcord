# East / West Security

The goal of this task is to implement east/west security in your microservice.
## Scope

In this task you will implement east/west security in your microservice. You will use JWT tokens with roles and claims to protect your internal endpoints.
## Task

Continue to configure the mappings on the API gateway for your microservice. Identify the endpoints that should be restricted and thus only accessible by other microservices.

Consider the following:

1. What mechanism will you use to authenticate the incoming requests?
2. What mechanism will you use to authorize the incoming requests?
3. How will you implement the authentication and authorization logic?

## Authentication mechanisms
- JWT tokens
- API keys
- OAuth
- Other (please specify which)

## Example: authentication and authorization with Ocelot

In your existing gateway, install *Ocelot* and *Microsoft.AspNetCore.Authentication.JwtBearer* versions compatible with your project's .NET version.

### Program.cs
```c#
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ocelot.json");
builder.Services.AddAuthentication()
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://your-identity-provider";
        options.Audience = "internal-api";
        options.MapInboundClaims = false; // Preserve the "role" claim name.
    });
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();
await app.UseOcelot();
await app.RunAsync();
```
### ocelot.json
```json
{
  "Routes": [
    {
      "UpstreamPathTemplate": "/internal/ping",
      "UpstreamHttpMethod": ["GET"],
      "DownstreamPathTemplate": "/ping",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": { "Host": "pingservice", "Port": 80 },
      "AuthenticationOptions": {
        "AuthenticationProviderKeys": ["Bearer"]
      },
      "RouteClaimsRequirement": {
        "role": "internal-service"
      }
    }
  ]
}
```
Replace the issuer, audience and downstream address with your setup. The trusted issuer must issue a token for *internal-api* with the *claim role: internal-service*. Send it as *Authorization: Bearer <access-token>*.

### Authentication: 
the Bearer scheme validates the JWT; a missing or invalid token returns 401. **Authorization**: *RouteClaimsRequirement* checks the role; a valid token without that role returns 403. With both, Ocelot forwards the request. Also protect the receiving service if it can be called directly.

Ocelot documentation: [Authentication](https://ocelot.readthedocs.io/en/latest/features/authentication.html)-[Authorization](https://ocelot.readthedocs.io/en/latest/features/authorization.html)

# HashiCorp Vault integration

The goal of this task is to integrate HashiCorp Vault in your microservice.
## Contents
- [Task]()
- [Running Vault]()
- [Initialization & unsealing]()
- [Creating a secret]()
- [Retrieving secrets in .NET]()
- [Testing]()

## Task

Use Vault to store a secret used by your microservice, such as an API key or database connection string. Retrieve it at startup instead of keeping it in your source code.

The example below uses Vault's HTTP API. You can also use the [.NET Vault Client](https://github.com/hashicorp/vault-client-dotnet).
## Running Vault
### Configuration

Create a directory for Vault and save this as `config.hcl`:
```json
storage "file" {
  path = "/vault/file"
}
listener "tcp" {
  address = "0.0.0.0:8200"
  tls_disable = 1
}
disable_mlock = true
api_addr = "http://vault:8200"
ui = true
```
### Docker Compose

Save this as `compose.yaml` in the same directory:
```yaml
services:
  vault:
    image: hashicorp/vault:1.21.0
    ports:
      - "127.0.0.1:8200:8200"
    volumes:
      - ./config.hcl:/vault/config/config.hcl:ro
      - vault-data:/vault/file
    command: server
    environment:
      VAULT_ADDR: http://127.0.0.1:8200
volumes:
  vault-data:
```
Start Vault from that directory:
```bash
docker compose config --quiet
docker compose up -d
docker compose logs vault
```
This setup is for local development. It uses HTTP and disables memory locking. The published port is only accessible from your computer. Use TLS and an appropriate server configuration for deployment.
## Initialization & unsealing

Open [http://localhost:8200](http://localhost:8200). Initialize Vault with **Key Shares = 3** and **Key Threshold = 2**.

Save the root token and unseal keys securely. Enter two different unseal keys to unseal Vault, then sign in with the root token.

Initialize Vault only once. After a restart, unseal it with the same keys. The named Docker volume keeps your data; `docker compose down -v` deletes it.
## Creating a secret

Open a shell in the Vault container and enter your root token at the login prompt:
```bash
docker compose exec vault sh
vault login
```
Enable the KV v2 secrets engine, store an example API key and create a policy that only allows reading that secret:
```shell
vault secrets enable -path=secret kv-v2
vault kv put secret/pingpong ApiKey=classroom-demo-only
printf 'path "secret/data/pingpong" { capabilities = ["read"] }' | vault policy write pingpong-read -
vault token create -policy=pingpong-read -no-default-policy -ttl=1h
exit
```
Enable `secret/` only once. If it already exists, check that it uses KV v2.

Copy the application token returned by `vault token create`. Use this token in your microservice. It expires after one hour and cannot write secrets or read other paths. Keep the root token for administration.
## Retrieving secrets in .NET

Create a console project to try the integration:

`dotnet new console -n VaultRead`

Replace `VaultRead/Program.cs` with:
```c#
using System.Net;
using System.Text.Json;

var address = Environment.GetEnvironmentVariable("VAULT_ADDR")
    ?? "http://localhost:8200";
var token = Environment.GetEnvironmentVariable("VAULT_TOKEN")
    ?? throw new InvalidOperationException("Set VAULT_TOKEN to the scoped application token.");
using var client = new HttpClient
{
    BaseAddress = new Uri(address.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromSeconds(5)
};
client.DefaultRequestHeaders.Add("X-Vault-Token", token);
using var response = await client.GetAsync("v1/secret/data/pingpong");
if (response.StatusCode == HttpStatusCode.Forbidden)
    throw new InvalidOperationException("Vault denied access: check token, TTL and policy.");
response.EnsureSuccessStatusCode();
using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
var apiKey = document.RootElement.GetProperty("data").GetProperty("data")
    .GetProperty("ApiKey").GetString();
if (string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException("Vault secret ApiKey is missing or empty.");
// Use apiKey to configure the downstream client; never log or return its value.
Console.WriteLine("Vault secret loaded successfully (value not displayed).");
```
Set the Vault address and application token in your terminal, then run the project.
### macOS / Linux
```ps
export VAULT_ADDR=http://localhost:8200
read -s VAULT_TOKEN
export VAULT_TOKEN
dotnet run --project VaultRead
```
After `read -s VAULT_TOKEN`, paste the application token and press Enter. The token is not displayed.
### Windows PowerShell
```ps
$env:VAULT_ADDR = "http://localhost:8200"
$secureToken = Read-Host "Application token" -AsSecureString
$env:VAULT_TOKEN = [System.Net.NetworkCredential]::new("", $secureToken).Password
dotnet run --project VaultRead
```
You should see `Vault secret loaded successfully (value not displayed)`.

KV v2 returns the secret under `data.data`. The HTTP path is `/v1/secret/data/pingpong`; the CLI path is `secret/pingpong`.
### In your microservice

Use the same retrieval code during startup and pass the secret to the client that needs it. Supply the token at runtime; do not commit tokens or print secret values.

If your microservice runs in Docker, put it on the same network as Vault and use `http://vault:8200`. Inside a container, `localhost` refers to that container.
## Testing

Check that your microservice can read the secret. Then try the following:

- Use an invalid token. Vault should return 403.
- Try reading another path or overwriting the secret with the application token. Both should return 403.
- Run `docker compose restart vault`. Requests should fail while Vault is sealed. Unseal it with two of your original keys and check that the secret is still there.

## Example repository

The [Ping/Pong vault branch](https://github.com/patrickstolc/ping-pong-microservices/tree/vault) shows a client-library integration. When adapting it, replace the hard-coded login credentials with runtime configuration and remove the secret logging.