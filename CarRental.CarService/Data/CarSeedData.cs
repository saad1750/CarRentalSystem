using CarRental.CarService.Models;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.CarService.Data;

// Testdata: en lille flåde på den ene udlejningslokation. Faste id'er, så
// Booking Service' testdata kan pege på de samme biler. Se DOCKER.md.
public static class CarSeedData
{
    public static readonly Guid ToyotaYarisId = Guid.Parse("22222222-2222-2222-2222-000000000001");
    public static readonly Guid VwGolfId = Guid.Parse("22222222-2222-2222-2222-000000000002");
    public static readonly Guid SkodaOctaviaId = Guid.Parse("22222222-2222-2222-2222-000000000003");
    public static readonly Guid KiaSportageId = Guid.Parse("22222222-2222-2222-2222-000000000004");
    public static readonly Guid VolvoXc90Id = Guid.Parse("22222222-2222-2222-2222-000000000005");
    public static readonly Guid MercedesEClassId = Guid.Parse("22222222-2222-2222-2222-000000000006");
    public static readonly Guid TeslaModel3Id = Guid.Parse("22222222-2222-2222-2222-000000000007");
    public static readonly Guid FordTransitId = Guid.Parse("22222222-2222-2222-2222-000000000008");

    private const string Location = "København";

    public static async Task SeedAsync(CarDbContext db)
    {
        if (await db.Cars.AnyAsync())
            return;

        db.Cars.AddRange(
            Car(ToyotaYarisId, "Toyota", "Yaris", CarType.Economy, 4, 299m),
            Car(VwGolfId, "Volkswagen", "Golf", CarType.Compact, 5, 399m),
            Car(SkodaOctaviaId, "Skoda", "Octavia Combi", CarType.Compact, 5, 449m),
            Car(KiaSportageId, "Kia", "Sportage", CarType.SUV, 5, 599m),
            Car(VolvoXc90Id, "Volvo", "XC90", CarType.SUV, 7, 899m),
            Car(MercedesEClassId, "Mercedes-Benz", "E-Class", CarType.Luxury, 5, 1199m),
            // Til test af at biler på værksted ikke kan søges/bookes.
            Car(TeslaModel3Id, "Tesla", "Model 3", CarType.Luxury, 5, 999m, CarAvailabilityStatus.InMaintenance),
            Car(FordTransitId, "Ford", "Transit Kombi", CarType.Van, 9, 799m));

        await db.SaveChangesAsync();
    }

    private static Car Car(Guid id, string make, string model, CarType type, int passengers, decimal price,
        CarAvailabilityStatus status = CarAvailabilityStatus.Available) => new()
    {
        Id = id, Make = make, Model = model, Type = type, PassengerCapacity = passengers,
        PricePerDay = price, Location = Location, Status = status
    };
}
