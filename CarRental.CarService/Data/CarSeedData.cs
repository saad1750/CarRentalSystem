using CarRental.CarService.Models;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.CarService.Data;

// Testdata: en lille flåde på den ene udlejningslokation, id 1-10.
// Status passer til bookingerne i Booking Service' testdata (udlejet/reserveret).
public static class CarSeedData
{
    private const string Location = "København";

    public static async Task SeedAsync(CarDbContext db)
    {
        if (await db.Cars.AnyAsync())
            return;

        db.Cars.AddRange(
            Car(1, "Peugeot", "208", CarType.Economy, 5, 279m),
            Car(2, "Fiat", "500", CarType.Economy, 4, 249m),
            Car(3, "Toyota", "Corolla", CarType.Compact, 5, 379m, CarAvailabilityStatus.RentedOut),  // booking 5 er afhentet
            Car(4, "Hyundai", "i30", CarType.Compact, 5, 359m, CarAvailabilityStatus.Reserved),       // booking 7 er bekræftet
            Car(5, "Audi", "Q5", CarType.SUV, 5, 749m, CarAvailabilityStatus.Reserved),               // booking 8 er bekræftet
            Car(6, "Toyota", "RAV4 Hybrid", CarType.SUV, 5, 649m),
            Car(7, "BMW", "5 Series", CarType.Luxury, 5, 1099m),
            Car(8, "Polestar", "2", CarType.Luxury, 5, 949m, CarAvailabilityStatus.InMaintenance),    // på værksted
            Car(9, "Mercedes-Benz", "Vito Tourer", CarType.Van, 8, 849m),
            Car(10, "Volkswagen", "Caravelle", CarType.Van, 9, 899m));

        await db.SaveChangesAsync();
        await db.ResetIdSequenceAsync("cars");
    }

    private static Car Car(int id, string make, string model, CarType type, int passengers, decimal price,
        CarAvailabilityStatus status = CarAvailabilityStatus.Available) => new()
    {
        Id = id, Make = make, Model = model, Type = type, PassengerCapacity = passengers,
        PricePerDay = price, Location = Location, Status = status
    };
}
