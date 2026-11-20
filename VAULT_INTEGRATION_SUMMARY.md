# Vault Integration for ApiGateway

## Overview
Integrated HashiCorp Vault secret retrieval for the JWT signing key in the ApiGateway microservice. The JWT signing key is now retrieved from Vault at startup and passed to the authentication system.

## Changes Made

### 1. New File: `apps/api-gateway/src/ApiGateway/Application/Services/VaultSecretProvider.cs`
- Created a new service to handle Vault secret retrieval
- Retrieves the `JwtSigningKey` from Vault's KV v2 engine at path `kv/data/secrets`
- Uses environment variables for Vault address and token
- Proper error handling for Vault access issues
- Never logs or exposes secret values

### 2. Modified: `apps/api-gateway/src/ApiGateway/Application/Services/JwtOptions.cs`
- Changed `AuthenticationProviderKey` from `required` to nullable (`string?`)
- This allows the key to be set at runtime from Vault instead of configuration

### 3. Modified: `apps/api-gateway/src/ApiGateway/Application/Services/JwtTokenService.cs`
- Added validation to ensure the JWT signing key is available before creating tokens
- Throws clear error if Vault secret was not retrieved properly

### 4. Modified: `apps/api-gateway/src/ApiGateway/Extensions/ServiceExtensions.cs`
- Removed direct reading of `AuthenticationProviderKey` from configuration
- Added `AddVaultSecretProvider()` extension method
- Set `IssuerSigningKey` to `null!` initially (will be configured at startup)
- Added comments explaining the signing key will be set from Vault

### 5. Modified: `apps/api-gateway/src/ApiGateway/Program.cs`
- Added `using System.Text;` and other required namespaces
- Registered `VaultSecretProvider` as a singleton service
- Added startup logic to:
  - Retrieve JWT signing key from Vault
  - Update `JwtOptions.AuthenticationProviderKey` with the Vault secret
  - Configure JWT bearer authentication with the signing key

### 6. Modified: `apps/api-gateway/src/ApiGateway/appsettings.json`
- Added `Vault` configuration section with default address `http://vault:8200`
- Removed `AuthenticationProviderKey` from JWT configuration (now comes from Vault)
- Kept other JWT settings (Issuer, Audience, ExpiryHours)

## Security Features

✅ **Token not committed**: Vault token is provided via environment variable `VAULT_TOKEN`
✅ **Secrets not logged**: No secret values are ever logged or printed
✅ **Fail-fast**: Application fails immediately if Vault is inaccessible or secrets are missing
✅ **Docker-ready**: Uses `http://vault:8200` for Docker network DNS resolution
✅ **Proper error handling**: Clear error messages for Vault access issues

## Usage

### Environment Variables
```bash
# Required - Vault authentication token
VAULT_TOKEN=your-vault-token-here

# Optional - Vault address (defaults to http://vault:8200)
VAULT_ADDR=http://vault:8200
```

### Docker Configuration
```yaml
services:
  api-gateway:
    environment:
      - VAULT_TOKEN=${VAULT_TOKEN}
      - VAULT_ADDR=http://vault:8200
    networks:
      - your-network

  vault:
    image: vault:latest
    networks:
      - your-network
```

### Vault Secret Structure
The secret should be stored in Vault at path `kv/data/secrets` with the following data:
```json
{
  "JwtSigningKey": "your-jwt-signing-key-here"
}
```

## How It Works

1. **Startup**: When the ApiGateway starts, it creates the `VaultSecretProvider` service
2. **Secret Retrieval**: After building the app, the JWT signing key is retrieved from Vault
3. **Configuration**: The signing key is injected into:
   - `JwtOptions.AuthenticationProviderKey` (for token creation)
   - JWT bearer authentication `IssuerSigningKey` (for token validation)
4. **Token Creation**: `JwtTokenService` uses the Vault-provided key to sign new tokens
5. **Token Validation**: JWT bearer middleware uses the Vault-provided key to validate incoming tokens

## Testing

To test the integration:
1. Start Vault server on the same Docker network
2. Create the secret: `vault kv put kv/data/secrets JwtSigningKey=your-key-here`
3. Set the `VAULT_TOKEN` environment variable with a valid token
4. Start the ApiGateway - it should successfully retrieve the key and start normally
5. If Vault is inaccessible or the secret is missing, the application will fail to start with a clear error message
