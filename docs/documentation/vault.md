# HashiCorp Vault (week 45, task 02)

## What is stored and who reads it
One secret: `JwtSigningKey` (HS256) in the KV v2 engine at `kv/secrets`.
- The API gateway signs tokens with it (dev faucet).
- ChatService validates the same tokens with it (East/West, see [east-west-security.md](east-west-security.md)).

Both read it once at startup over Vault's HTTP API (`GET /v1/kv/data/secrets`, value under `data.data`) and never log it. Code: `VaultSecretProvider` in
[`apps/api-gateway/src/ApiGateway/Application/Services/`](../../apps/api-gateway/src/ApiGateway/Application/Services/VaultSecretProvider.cs) and
[`apps/chat-microservice/src/ChatService/Infrastructure/Web/`](../../apps/chat-microservice/src/ChatService/Infrastructure/Web/VaultSecretProvider.cs).

## Design choices
- **Least privilege:** the policy [`vault/policies/secrets-reader.hcl`](../../vault/policies/secrets-reader.hcl) allows `read` on `kv/data/secrets` only – no list, no write, no other paths.
- **One token per service:** the gateway (`VAULT_TOKEN`) and ChatService (`CHATSERVICE_VAULT_TOKEN`) share the policy but have their own tokens, so one can be revoked without stopping the other.
- **Token at runtime, not in git:** compose reads the tokens from `.env` (git-ignored). Without them the rest of the stack still starts; only the two services stop with "Vault token is required".
- **Fixed dev token ids:** `.env.example` holds made-up ids that README step 3 creates in Vault, so a fresh clone works without editing `.env`. In a real environment Vault generates the token and it is delivered as a secret.
- **CLI path vs HTTP path:** with KV v2 the CLI uses `kv/secrets`; the HTTP API and policies use `kv/data/secrets`.

## Setup
See [README → "Første gang: Vault og `.env`"](../../README.md#første-gang-vault-og-env) (init with 3 key shares / threshold 2, unseal, secret, policy, tokens).

## Tests
Run on 24 Nov 2026 against a fresh Vault in a separate compose project (`docker compose -p bzverify ...`), following the README steps.

| Test | Result |
|---|---|
| Gateway token / ChatService token reads `v1/kv/data/secrets` | 200 / 200 |
| Invalid token | 403 |
| App token: read another path / list metadata / write the secret / create a token | 403 / 403 / 403 / 403 |
| Stack without `.env` | Rest of the stack starts; gateway and ChatService exit with "Vault token is required" |
| Stack with `.env` | Faucet 200; gateway → ChatService `/chat/health` (internal-service) 200 |
| `docker compose restart vault` | Reads return 503 (sealed); gateway and ChatService exit at startup with `HttpRequestException: 503` |
| Unseal with two of the three keys, start the services | Secret still there (200); `/chat/health` 200 |

## Known limitations
- Database passwords are still in plain text in `docker-compose.yml`; only the JWT signing key is in Vault.
- Vault runs over HTTP (no TLS) – local development only.
- App tokens expire after 32 days and must be recreated (README step 3).
- A sealed Vault makes the services exit with a raw `503` instead of a clear message.
