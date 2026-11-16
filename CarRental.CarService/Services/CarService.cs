using CarRental.CarService.Data;
using CarRental.CarService.Models;
using CarRental.Shared.DTOs;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.CarService.Services;

// Gemmer biler i PostgreSQL via EF Core (CarDbContext).
public class CarService : ICarService
{
    private readonly CarDbContext _db;

    public CarService(CarDbContext db)
    {
        _db = db;
    }

    // Krav: søgning/filtrering på lokation, dato, biltype.
    public Task<List<Car>> SearchAsync(string? location, CarType? type, DateTime? startDate, DateTime? endDate)
    {
        var query = _db.Cars.AsNoTracking().Where(c => c.Status == CarAvailabilityStatus.Available);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var normalized = location.Trim().ToLower();
            query = query.Where(c => c.Location.ToLower() == normalized);
        }

        if (type.HasValue)
            query = query.Where(c => c.Type == type.Value);

        // start/slut-dato tjekkes ikke her endnu — det kræver et kald til Booking Service.

        return query.OrderBy(c => c.PricePerDay).ToListAsync();
    }

    // Krav: admin kan tilføje/opdatere/fjerne biler.
    public async Task<Car> AddCarAsync(Car car)
    {
        car.Id = 0; // databasen tildeler næste ledige id

        _db.Cars.Add(car);
        await _db.SaveChangesAsync();
        return car;
    }

    public async Task<Car?> UpdateCarAsync(Car car)
    {
        var existing = await _db.Cars.FirstOrDefaultAsync(c => c.Id == car.Id);
        if (existing is null)
            return null;

        // Kopiér felterne over på den indlæste bil. Så bruger concurrency-tjekket
        // databasens version og ikke det, klienten sendte.
        existing.Make = car.Make;
        existing.Model = car.Model;
        existing.Type = car.Type;
        existing.PassengerCapacity = car.PassengerCapacity;
        existing.PricePerDay = car.PricePerDay;
        existing.Location = car.Location;
        existing.Status = car.Status;

        await _db.SaveChangesAsync(); // DbUpdateConcurrencyException hvis bilen er ændret imens
        return existing;
    }

    public async Task<bool> RemoveCarAsync(int carId)
    {
        var deleted = await _db.Cars.Where(c => c.Id == carId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    // Krav: realtidstjek af tilgængelighed.
    public async Task<CarAvailabilityResponseDto> CheckAvailabilityAsync(CarAvailabilityRequestDto request)
    {
        var car = await _db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CarId);

        return new CarAvailabilityResponseDto
        {
            CarId = request.CarId,
            IsAvailable = car is not null && car.Status == CarAvailabilityStatus.Available,
            PricePerDay = car?.PricePerDay ?? 0m
        };
    }

    // Krav: først-til-mølle, aldrig double booking.
    // Én atomisk UPDATE ... WHERE Status = 'Available' i databasen: kun den første
    // af to samtidige forespørgsler ændrer rækken, den anden rammer 0 rækker.
    // Virker også når der kører flere instanser af Car Service.
    public async Task<bool> TryReserveAsync(int carId)
    {
        var updated = await _db.Cars
            .Where(c => c.Id == carId && c.Status == CarAvailabilityStatus.Available)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CarAvailabilityStatus.Reserved));

        return updated == 1;
    }

    // Kaldes af Booking Service ved annullering, fejlet betaling eller aflevering.
    // Biler på værksted røres ikke.
    public async Task ReleaseAsync(int carId)
    {
        await _db.Cars
            .Where(c => c.Id == carId && c.Status != CarAvailabilityStatus.InMaintenance)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CarAvailabilityStatus.Available));
    }

    // Simpel regelbaseret stand-in for AI-anbefalingen — skift senere til rigtigt kald.
    public Task<List<Car>> GetAiRecommendationsAsync(int passengerCount, decimal maxPricePerDay)
    {
        return _db.Cars.AsNoTracking()
            .Where(c => c.Status == CarAvailabilityStatus.Available)
            .Where(c => c.PassengerCapacity >= passengerCount)
            .Where(c => c.PricePerDay <= maxPricePerDay)
            .OrderBy(c => c.PricePerDay)
            .ToListAsync();
    }
}
