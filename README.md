# Bizcord

Bizcord er en Discord-lignende chatplatform opdelt i bounded contexts. Dette repo implementerer to af dem: **ChatService** (direkte og gruppechats, beskeder, kvitteringer) og **Real-Time Communication** (live levering af beskeder til online brugere via SignalR). Services kommunikerer via REST og RabbitMQ.

Analyse, design og arkitekturbeslutninger: [docs/analysis-and-design.md](docs/analysis-and-design.md)
## Kør projektet

```bash
docker compose up -d --build
```

| Komponent | Port |
|-----------|------|
| ChatService | 8081 |
| RTC | 8082 |
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