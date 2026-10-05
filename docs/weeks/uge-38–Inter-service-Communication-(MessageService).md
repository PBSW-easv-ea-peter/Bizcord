# SI Uge 38 – Inter-service Communication (MessageService)

Sep 21, 2026 · @Peter Ilsted Bech

## Kontekst og arbejdsmåde

Fag: System Integration (SI). Emne: Inter-service communication. Jeres gruppe ejer **MessageService** (ny service, som skal bygges fra bunden sammen med MessageDB) i BizCord-systemet. I skal forestille jer, at andre teams ejer de øvrige services (User, Channel osv.).

Ifølge C4-modellen (Structurizr) for Bizcord: **MessageService** + **MessageDB** er servicen, I skal bygge — den findes endnu ikke i repoet. Den skal publicere events (fx en besked sendt) til den delte **RabbitMQ**-broker, som ligger uden for alle bounded contexts (ingen af teamsene ejer den). **Real-Time Communication Service** er en anden, allerede delvist implementeret service, der abonnerer på RabbitMQ og skal pushe notifikationer videre til klienterne — lige nu logger den blot det modtagne (kendt gap: ingen SignalR/WebSocket-push endnu). Den har allerede boilerplate for et `IMessageClient` (EasyNetQ-baseret) fra uge 37 lab 3, inkl. en `HandleMessages`-BackgroundService, der pt. kun håndterer én beskedtype (`PingMessage`).

Den centrale pointe fra Patricks svar: I arbejder i isolation, men designer kontrakten som om I er ét hold blandt flere. Konkret betyder det:

- Lav IKKE én stor domænemodel for hele BizCord og hak den bagefter op i services — det ender som en distribueret monolit.
- Fokusér på ét domæne/bounded context ad gangen: MessageService.
- I skal ikke selv implementere de services, MessageService kommunikerer med (UserService, ChannelService osv.) — referér blot til fx `userId` og `channelId` og antag, de kommer udefra.
- Nøglespørgsmål at stille jer selv undervejs:
  1. Hvad er MessageService ansvarlig for?
  2. Hvilke data ejer den?
  3. Hvilke operationer skal andre kunne bede den om at udføre?
  4. Hvilke informationer/events skal den sende videre, når noget relevant er sket?
  5. Hvilken information har den brug for fra andre services?

"Færdig" betyder her: ansvar og kontrakt er tydelige — ikke at hele BizCord er bygget. En simpel første iteration (fx blot `name` + `message`) er fin, hvis den er nok til at vise flowet. Kontrakten er levende og må ændre sig, i takt med at I bliver klogere — ligesom et første database-schema.

## Task 01 – Implement your domain model


Mål: begynde implementeringen af MessageService ved at modellere domænet.

**Arkitektur:** frit valg — layered architecture (som i undervisningens ProductApi-eksempel) er inspiration, ikke krav.

**Modellering:** domænemodellen skal være teknologi-agnostisk og beskrive selve domænet, ikke en teknisk løsning. Byggeklodser:

- **Entities** — objekter med egen identitet, unikt id og livscyklus. Hver microservice må gerne have sin egen private version af en entitet (fx både MessageService og ChannelService kan have deres eget begreb om en "kanal").
- **Value objects** — immutable værdier uden identitet (datoer, tidspunkter, koordinater, valuta osv.).
- **Domain events** — notifikationer til andre services om at noget er sket (fx en besked er sendt, et kanalnavn er ændret). Services kan både producere og konsumere events.

Fokus i denne opgave er primært på entities og value objects. Modellen er et levende dokument — jeres interne repræsentation — og forventes at ændre sig over tid.

## Task 02 – Implement a shared model

Mål: lav en model af domænet, der kan deles med andre microservices — den faktiske kontrakt MessageService stiller til rådighed for omverdenen.

**Data contracts:** medtag kun de felter, andre services har brug for til integration. Eksempel på en delt `Channel`-model:

| Felt | Formål |
| --- | --- |
| Id | Unik identifikation |
| Name | Visningsnavn |
| Description | Kontekst for forbrugeren |
| CreatedAt | Tidsstempel |

Det er nok til fx en notifikationsservice — uden at den kender interne detaljer.

**Skjul interne detaljer:** den delte model skal ikke afsløre intern kompleksitet. Eksempel: internt kan en service have et komplekst rollebaseret adgangssystem (roller i flere tabeller, permission-overrides pr. kanal, audit-log af rollewændringer) — den delte model kan nøjes med et `RoleType`-felt (Admin/Moderator/Member) og et `Permissions`-flag (fx `canManageChannels`).

## Task 03 – Implement a REST API for your microservice

Mål: en REST API, så andre microservices kan interagere med MessageService. Udgangspunktet er CRUD:

| Metode | Ansvar |
| --- | --- |
| POST | Opret en ny entitet |
| GET | Læs én eller flere entiteter |
| PUT | Opdatér en eksisterende entitet |
| DELETE | Slet en eksisterende entitet |

Husk undervejs:

1. Adskil business logic fra præsentationslogik.
2. Brug HTTP-metoder og statuskoder konsistent i både requests og responses.
3. Brug dependency injection.
4. Validér input.

## (Ekstra) Discovery og auto-registrering af message handlers

Mål: gør det nemmere at implementere, vedligeholde og integrere message handlers.

**Den naive tilgang:** en `BackgroundService`, der kører sammen med web API'ets thread(s) og håndterer én bestemt beskedtype (se `MessageHandler.cs`-eksemplet fra dotnet-web-api-and-rabbitmq).

**Opgaven:** den naive tilgang skalerer ikke og er svær at vedligeholde, når der er flere beskedtyper. I skal selv finde en måde at automatisk opdage og registrere message handlers på — metoden er ikke givet.

## Åbne spørgsmål til sparring

- I `apps/` findes kun `real-time-communication-microservice` endnu. Skal en ny app (fx `message-microservice`) oprettes efter samme mønster (uge 37 lab 1+2: `src/`, `tests/`, `Dockerfile`, `README`)?
- Skal MessageService's `IMessageClient` designes efter samme mønster som Real-Time Communication Service's (interface + EasyNetQ-implementering), eller er det en fri, uafhængig implementering?
- Ekstraopgaven om discovery af message handlers — er den mest relevant for jeres egen MessageService (hvis den også skal konsumere events), eller for Real-Time Communication Service, hvis `HandleMessages` i dag kun håndterer én beskedtype (`PingMessage`)?
- Hvilke(t) events skal MessageService publicere til RabbitMQ (fx et `MessageSent`-event), og hvilket payload-format forventer forbrugere som Real-Time Communication Service?
- Hvor detaljeret skal domænemodellen være i denne omgang, hvis `name` + `message` er nok til at vise flowet?
