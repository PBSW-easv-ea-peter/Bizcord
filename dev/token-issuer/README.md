# Dev token issuer

A minimal JWT issuer for local development and tests. It stands in for UserAuth and follows [the token contract](../../docs/documentation/token-contract.md).

> **Dev only.** It issues any token, including `internal-service`, to anyone and without login. It only starts with the compose profile `dev` and is bound to `127.0.0.1`. Never deploy it.

## Run

```sh
docker compose --profile dev up -d --build token-issuer
```

The issuer is then available at `http://localhost:8090`. From the gateway container it is `http://host.docker.internal:8090`, and from the Bizcord containers it is `http://token-issuer:8080`.

## Endpoints

| Endpoint | Purpose |
|---|---|
| `POST /token` | Issues a token. Body: `{"sub": "<guid or service name>", "role": "member" \| "internal-service"}` |
| `GET /.well-known/openid-configuration` | Discovery. Validators point `MetadataAddress` here |
| `GET /.well-known/jwks.json` | Public signing key |

The audience is derived from the role: `member` gives `bizcord`, and `internal-service` gives `bizcord-internal`.

```sh
curl -s -X POST http://localhost:8090/token \
  -H "Content-Type: application/json" \
  -d '{"sub":"11111111-1111-1111-1111-111111111111","role":"member"}'
```

## Keys

A new RSA key is generated on every start and is only kept in memory. A restart therefore invalidates all issued tokens. Validators fetch the new key when they see an unknown `kid`, but JwtBearer throttles that refresh (`RefreshInterval`, 5 minutes by default). Right after a restart, validators can reject new tokens for a few minutes. Lower `RefreshInterval` in development if this gets in the way.
