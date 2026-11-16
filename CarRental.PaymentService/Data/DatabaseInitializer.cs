using Microsoft.EntityFrameworkCore;

namespace CarRental.PaymentService.Data;

// Køres én gang ved opstart (se Program.cs):
//  1. venter på at databasen er klar (Postgres-containeren kan være lidt længere om at starte)
//  2. opretter tabellerne ud fra DbContext-modellen, hvis de ikke findes
//  3. indsætter testdata, hvis "Database:SeedTestData" er true og tabellerne er tomme
//
// NB: EnsureCreated laver skemaet første gang, men opdaterer det ikke ved senere
// ændringer i modellen. Ændres modellen, så nulstil databasen med
// "docker compose down -v". Før deployment skiftes til EF Core Migrations.
public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task InitializeDatabaseAsync<TContext>(
        this WebApplication app,
        Func<TContext, Task> seed)
        where TContext : DbContext
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var seedTestData = app.Configuration.GetValue("Database:SeedTestData", false);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();

                var created = await db.Database.EnsureCreatedAsync();
                logger.LogInformation(created
                    ? "Database schema created for {Context}"
                    : "Database schema already exists for {Context}", typeof(TContext).Name);

                if (seedTestData)
                    await seed(db);

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning("Database not ready ({Message}), retry {Attempt}/{Max} in {Delay}s",
                    ex.Message, attempt, MaxAttempts, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay);
            }
        }
    }
}
