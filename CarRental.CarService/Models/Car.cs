using System.Text.Json.Serialization;
using CarRental.Shared.Enums;

namespace CarRental.CarService.Models;

// Krav: søgning/filtrering, admin flådestyring, pris pr. dag efter biltype.
public class Car
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public CarType Type { get; set; }

    // Bruges af AI-anbefalingen til at matche gruppestørrelse.
    public int PassengerCapacity { get; set; }

    public decimal PricePerDay { get; set; }
    public string Location { get; set; } = string.Empty;

    // Flippes atomisk af TryReserveAsync/ReleaseAsync.
    public CarAvailabilityStatus Status { get; set; } = CarAvailabilityStatus.Available;

    // Optimistic concurrency: PostgreSQL's systemkolonne "xmin" ændres ved hver
    // opdatering. Har en anden ændret bilen imellem, afviser EF Core gemningen.
    // Styres af databasen, derfor ikke en del af API'et.
    [JsonIgnore]
    public uint Version { get; set; }
}
