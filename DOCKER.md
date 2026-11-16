# Docker – udviklingsmiljø

![Udviklingsmiljø](docs/dev-environment.png)

## Hvad er der

| Fil | Formål |
|---|---|
| `CarRental.*Service/Dockerfile` | Multi-stage build pr. service (SDK-image bygger, lille ASP.NET-runtime-image kører, non-root) |
| `docker-compose.yml` | 5 services + 4 PostgreSQL-databaser (én pr. service) + 1 RabbitMQ, eget netværk og named volumes |
| `.env.example` → `.env` | Porte, passwords, image-tag. `.env` er git-ignored |
| `.dockerignore` | Holder `bin/`, `obj/`, `.git` osv. ude af build-context |
| `.github/workflows/docker-publish.yml` | CI/CD: bygger alle images og pusher til ghcr.io ved push til `main` |
| `docs/dev-environment.mmd/.png` | Diagram over opsætningen |

## Kom i gang

```bash
cp .env.example .env          # ret passwords
docker compose up -d --build  # byg og start alt
docker compose ps             # status
docker compose logs -f booking-service
docker compose down           # stop (tilføj -v for at slette DB-data)
```

| Service | URL |
|---|---|
| User | http://localhost:6001/swagger |
| Car | http://localhost:6002/swagger |
| Booking | http://localhost:6003/swagger |
| Notification | http://localhost:6004/swagger |
| Payment | http://localhost:6005/swagger |
| RabbitMQ UI | http://localhost:15672 (login fra `.env`) – se køer under *Queues* |

## Vigtige designvalg

- **Build context er repo-roden.** Hver Dockerfile bygger først `CarRental.Shared` til `local-nuget-feed/` inde i build-stagen, så `PackageReference` til `CarRental.Shared 1.0.0` virker præcis som lokalt. Den lokale `local-nuget-feed/` kopieres ikke ind.
- **Layer caching:** `.csproj` kopieres og restores før resten af koden, så NuGet-restore kun køres igen, når dependencies ændres.
- **HTTP i containerne (port 8080).** HTTPS bruges ikke internt i dev-miljøet. `UseHttpsRedirection()` gør ingenting, når der ikke er en HTTPS-port (giver kun en advarsel i loggen).
- **Docker DNS:** `appsettings.json` peger på `https://localhost:500x`. I compose overskrives det med miljøvariabler (`Services__CarService=http://car-service:8080` osv.), så servicenavne bruges i stedet for IP'er.
- **Databaser og broker** starter med healthchecks, og services venter på dem (`depends_on: condition: service_healthy`).

## RabbitMQ

| Event (routing key) | Publiceres af | Kø | Consumer |
|---|---|---|---|
| `payment.succeeded` | Payment | `booking-service.payment-events` | Booking → bekræfter booking |
| `payment.failed` | Payment | `booking-service.payment-events` | Booking → Failed + frigiver bil |
| `booking.confirmed` | Booking | `notification-service.booking-confirmed` | Notification → email/SMS |

- Alt publiceres til topic-exchangen `carrental.events`. Hver consumer har sin egen durable kø, så nye services kan lytte med uden at ændre afsenderen.
- Beskeder er persistente og ack'es først efter behandling: er en consumer nede, venter beskederne i køen.
- Fejler behandlingen, ender beskeden i `carrental.dead-letter` (se den i RabbitMQ-UI'et).
- Booking → User og Booking → Car er stadig REST, fordi Booking skal bruge svaret med det samme.

## Databaser

| Service | Database | Container | Port på host (fra Rider / DB-værktøj) |
|---|---|---|---|
| User | `userdb` | `user-db` | `localhost:5433` |
| Car | `cardb` | `car-db` | `localhost:5434` |
| Booking | `bookingdb` | `booking-db` | `localhost:5435` |
| Payment | `paymentdb` | `payment-db` | `localhost:5436` |

Login: `carrental` / `skift-mig` (fra `.env`). Ved opstart opretter hver service selv sine tabeller og, hvis `SEED_TEST_DATA=true`, testdataene nedenfor. Data ligger i named volumes og overlever `docker compose down`.

**Nulstil alt (fx efter ændring i en model-klasse):**
```bash
docker compose down -v      # sletter også databaserne
docker compose up -d --build
```

**Kør én service fra Rider mod resten i Docker:** stop den i Docker (`docker compose stop car-service`) og start den fra Rider. `appsettings.json` peger allerede på databasen via host-porten ovenfor og på RabbitMQ via `localhost:5672`.

## Testdata

Alle brugere har kodeordet **`Test123!`**. Id'erne er faste, så de kan bruges direkte i Swagger.

**Brugere** (User Service)

| Id | Navn | Email | Rolle | Til test af |
|---|---|---|---|---|
| `11111111-1111-1111-1111-000000000001` | Anna Jensen | anna@test.dk | Customer | Må leje (har booking-historik) |
| `11111111-1111-1111-1111-000000000002` | Mikkel Larsen | mikkel@test.dk | Customer | Afvises: under 21 |
| `11111111-1111-1111-1111-000000000003` | Sara Nielsen | sara@test.dk | Customer | Afvises: udløbet kørekort |
| `11111111-1111-1111-1111-000000000100` | Admin Bruger | admin@carrental.dk | Admin | |
| `11111111-1111-1111-1111-000000000200` | Jonas Medarbejder | employee@carrental.dk | Employee | |

**Biler** (Car Service) – alle i København

| Id | Bil | Type | Pladser | Pris/dag | Status |
|---|---|---|---|---|---|
| `22222222-2222-2222-2222-000000000001` | Toyota Yaris | Economy | 4 | 299 | Available |
| `22222222-2222-2222-2222-000000000002` | Volkswagen Golf | Compact | 5 | 399 | Available |
| `22222222-2222-2222-2222-000000000003` | Skoda Octavia Combi | Compact | 5 | 449 | Available |
| `22222222-2222-2222-2222-000000000004` | Kia Sportage | SUV | 5 | 599 | Available |
| `22222222-2222-2222-2222-000000000005` | Volvo XC90 | SUV | 7 | 899 | Available |
| `22222222-2222-2222-2222-000000000006` | Mercedes-Benz E-Class | Luxury | 5 | 1199 | Available |
| `22222222-2222-2222-2222-000000000007` | Tesla Model 3 | Luxury | 5 | 999 | InMaintenance (kan ikke bookes) |
| `22222222-2222-2222-2222-000000000008` | Ford Transit Kombi | Van | 9 | 799 | Available |

**Bookinger og betalinger** (Annas historik)

| Id | Bil | Periode | Status | Betaling |
|---|---|---|---|---|
| `33333333-3333-3333-3333-000000000001` | VW Golf | 1.–4. okt. 2026 | Completed | `44444444-4444-4444-4444-000000000001` (1.197 kr, Succeeded) |
| `33333333-3333-3333-3333-000000000002` | Kia Sportage | 15.–17. okt. 2026 | CancelledFree | – |

**Hurtig test i Swagger:** book `POST /api/booking` (port 6003) med Annas id og Toyota Yaris, og betal med `POST /api/payment` (port 6005). Prøv samme bil med Mikkel eller Sara, eller Teslaen, for at se reglerne afvise.

## CI/CD

Workflowet kører på GitHub ved push/PR til `main`. Én job pr. service (matrix). PR'er bygges kun, mens push til `main` pusher til
`ghcr.io/<owner>/<repo>/<service>:latest` og `:<commit-sha>`. Det kræver ingen secrets (bruger `GITHUB_TOKEN`).
Sæt `IMAGE_REGISTRY` i `.env` til samme sti, hvis I vil køre de færdige images med `docker compose pull && docker compose up -d`.
