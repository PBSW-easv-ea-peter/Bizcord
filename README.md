# Bizcord

Bizcord er en Discord-lignende chatplatform opdelt i bounded contexts. Dette repo implementerer to af dem: **ChatService** (direkte og gruppechats, beskeder, kvitteringer) og **Real-Time Communication** (live levering af beskeder til online brugere via SignalR). Services kommunikerer via REST og RabbitMQ.

Analyse, design og arkitekturbeslutninger: [docs/analysis-and-design.md](docs/analysis-and-design.md)
## Kør projektet

API-gatewayen henter sin JWT-signeringsnøgle fra Vault og skal bruge et Vault-token, som ligger i en lokal `.env`-fil. `.env` er git-ignoreret. Skabelonen `.env.example` er versioneret.

Uden `.env` starter resten af stakken, men gatewayen stopper med `Vault token is required`.

### Første gang: Vault og `.env`

1. Opret din lokale environment-fil ud fra skabelonen:
   ```bash
   cp .env.example .env
   ```
2. Start Vault, og initialisér og unseal den. Gem de 3 unseal-nøgler og root-tokenet et sikkert sted (ikke i git):
   ```bash
   docker compose up -d vault
   docker compose exec vault vault operator init -key-shares=3 -key-threshold=2
   docker compose exec vault vault operator unseal   # indsæt nøgle 1
   docker compose exec vault vault operator unseal   # indsæt nøgle 2
   ```
3. Opret secret, policy og gatewayens app-token. Tokenet får det faste id fra `.env.example`, så `.env` ikke skal redigeres:
   ```bash
   docker compose exec vault sh
   vault login                                    # root-token
   vault secrets enable -path=kv kv-v2
   vault kv put kv/secrets JwtSigningKey="$(head -c 48 /dev/urandom | base64)"
   vault policy write secrets-reader /vault/policies/secrets-reader.hcl
   vault token create -id=bizcord-gateway-dev-token -policy=secrets-reader -no-default-policy -ttl=768h
   exit
   ```
4. Start hele stakken:
   ```bash
   docker compose up -d --build
   ```

Vault gemmer sine data i volumen `vault-data`, så trin 2-3 skal kun gøres én gang. Efter en genstart af Vault-containeren er den sealed igen. Kør så de to `unseal`-kommandoer igen, og genstart derefter gatewayen.

Tokenet udløber efter 32 dage (Vaults standard-maksimum). Opret det så igen med `vault token create`-linjen fra trin 3. Det faste token-id er kun til udvikling: i et rigtigt miljø genererer Vault tokenet, og det leveres som en hemmelighed.

Policyen (`vault/policies/secrets-reader.hcl`) giver kun læseadgang til `kv/data/secrets`.

### Kør

```bash
docker compose up -d --build
```

| Komponent | Port |
|-----------|------|
| ChatService | 8081 |
| RTC | 8082 |
| FileService | 8083 |
| API Gateway | 8080 |
| Vault UI | 8200 |
| RabbitMQ UI | 15672 |

Logging (Fluentd-sidecar)

Alle services logger JSON til stdout (libs/Bizcord.Logging). En Fluentd-container læser Dockers logfiler, beholder kun linjer fra services, der har meldt sig til, og skriver dem samlet til sin egen konsol.

Se de samlede logs:
```bash
docker compose logs -f fluentd
```
En ny service melder sig til ved at tilføje dette i docker-compose.yml:
```yaml
    labels:
      logging: "fluentd"
    logging:
      driver: json-file
      options:
        labels: logging
```
Konfiguration: fluentd/fluent.conf. Designvalg: docs/documentation/logging-sidecar.md

GraphQL (ChatService)

ChatService har et GraphQL-lag ved siden af REST, bygget på de samme application services. Åbn http://localhost:8081/graphql i browseren for at få Nitro (GraphQL IDE). Kald kræver headeren `X-User-Id`, ligesom REST.

Test-guide med header, testdata, queries og variables samt designvalg: [docs/documentation/graphql.md](docs/documentation/graphql.md)