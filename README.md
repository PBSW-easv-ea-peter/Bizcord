# Bizcord

Bizcord er en Discord-lignende chatplatform opdelt i bounded contexts. Dette repo implementerer to af dem: **ChatService** (direkte og gruppechats, beskeder, kvitteringer) og **Real-Time Communication** (live levering af beskeder til online brugere via SignalR). Services kommunikerer via REST og RabbitMQ.

Analyse, design og arkitekturbeslutninger: [docs/analysis-and-design.md](docs/analysis-and-design.md)
