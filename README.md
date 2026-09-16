# Car Rental System — Development of Large Systems

Microservices solution for the semester project.

## Solution files — one per service, on purpose

Each microservice has **its own `.sln` file** inside its own folder (e.g.
`CarRental.BookingService/CarRental.BookingService.sln`), referencing only
itself. `CarRentalSystem.sln` in the repo root still exists and references
all 6 projects, for running/testing the whole system together.

**Why:** a `.sln` file is a plain text file listing project GUIDs. If everyone
on the team opens and edits the one shared root solution, unrelated changes
can touch that same file and cause git merge conflicts — even when nobody
touched each other's actual code. With a separate `.sln` per service, whoever
is working on Booking Service opens `CarRental.BookingService.sln` and never
touches the file someone else working on Car Service is using.

**Day-to-day workflow:** open your own service's `.sln` (e.g.
`CarRental.BookingService.sln`) to work on that service. Open the root
`CarRentalSystem.sln` only when you need to run several services together.

## CarRental.Shared is a NuGet package, not a project reference

The 5 services depend on `CarRental.Shared` (enums/DTOs/events) via
`<PackageReference Include="CarRental.Shared" Version="1.0.0" />` in their
`.csproj`, **not** `<ProjectReference>`. `CarRental.Shared.csproj` is set to
build itself into a `.nupkg` on every build (`GeneratePackageOnBuild`) and
drop it into `local-nuget-feed/` at the repo root; `nuget.config` at the repo
root registers that folder as a package source so the other services can
restore it without needing an external NuGet server.

**Why:** a plain `ProjectReference` means every service's build depends on
whatever Shared's source code looks like *right now* — a change to Shared by
one teammate can silently break another service's build the moment they pull.
A versioned package means each service depends on a specific, deliberate
release of Shared (e.g. `1.0.0`), which is what "each microservice must be
independently deployable" actually requires.

**⚠️ Required build order — read this before building anything:**
1. Build `CarRental.Shared` first (open its `.sln`... it doesn't have one on
   its own since nothing runs it directly — just build it from the root
   `CarRentalSystem.sln`, or `dotnet build CarRental.Shared`). This creates
   `local-nuget-feed/CarRental.Shared.1.0.0.nupkg`.
2. *Only after that* can the 5 services restore/build, since their
   `PackageReference` needs that `.nupkg` to already exist.
3. Whenever you change something in `CarRental.Shared`, bump `<Version>` in
   `CarRental.Shared.csproj`, rebuild it, then bump the `<PackageReference>`
   version in whichever service(s) should pick up the change and rebuild them.

`local-nuget-feed/` is git-ignored (it's a build output, not source) — every
teammate generates their own copy locally by building `CarRental.Shared`
after cloning.

## Projects and ports

| Project | Port (HTTPS) | Own database |
|---|---|---|
| CarRental.UserService | 5001 | Yes (later) |
| CarRental.CarService | 5002 | Yes (later) |
| CarRental.BookingService | 5003 | Yes (later) |
| CarRental.NotificationService | 5004 | No — by design |
| CarRental.PaymentService | 5005 | Yes (later) |
| CarRental.Shared | — (NuGet package, not a runnable service) | — |

Each service is its own ASP.NET Core Web API project so they can run and be deployed independently. Swagger is enabled in Development at `/swagger` on each service's port.

## Current storage

All services use in-memory storage (`ConcurrentDictionary`/`List` behind the interfaces) for now, so the whole solution runs without a database. When the database is added, only the class implementing e.g. `IBookingService` needs to change to use an injected EF Core `DbContext` — controllers and interfaces stay the same.

## Cross-service calls (current, temporary REST wiring)

- Booking Service → Car Service: check/reserve/release availability
- Booking Service → User Service: eligibility check (age 21+, valid licence)
- Payment Service → Booking Service: confirm booking / report payment failure
- Booking Service → Notification Service: booking confirmed

These synchronous REST calls between Booking↔Payment↔Notification stand in for what should eventually be **asynchronous messaging** (RabbitMQ/Kafka) — marked `TEMPORARY` in the code (`HttpEventPublisher`, `PaymentEventsController`, `NotificationController`). This is a good discussion point for the "communication styles" and "architectural patterns" sections of the report.

## Requirement → code map

- Age 21+ / valid licence → `Customer.IsEligibleToRent` (UserService), checked via `IUserServiceClient` before booking
- Real-time availability / no double booking → `CarService.TryReserveAsync` (atomic per-car lock; becomes an EF Core concurrency token later)
- First-completed-reservation-wins → same atomic reservation in `CarService`
- Duplicate booking check → `BookingService.HasOverlappingBookingAsync`
- Price per day by car type → `Booking.TotalPrice`, `Car.PricePerDay`
- Free cancellation ≥48h / 25% fee <48h → `Booking.IsWithinFreeCancellationWindow`, `BookingService.CancelBookingAsync`
- No cancellation after pickup → `Booking.CanBeCancelled`
- Payment failure cancels reservation → `PaymentService.ProcessPaymentAsync` → `IBookingServiceClient.NotifyPaymentFailedAsync` → `BookingService.HandlePaymentFailedAsync`
- Notification retries, never blocks booking → `NotificationService.SendWithRetryAsync`
- AI recommendation by passengers/price → `CarService.GetAiRecommendationsAsync` (rule-based placeholder — swap for a real model call later)

## Next steps

1. Add EF Core + a database per service (Code-First from the existing model classes)
2. Replace the `TEMPORARY` REST calls between Booking/Payment/Notification with real messaging
3. Add authentication (JWT) to `AuthController`
4. Add a frontend project talking to these APIs over REST/GraphQL
