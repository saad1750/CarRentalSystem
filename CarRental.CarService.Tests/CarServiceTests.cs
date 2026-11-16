using CarRental.CarService.Data;
using CarRental.CarService.Models;
using CarRental.Shared.DTOs;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using CarSvc = CarRental.CarService.Services.CarService;

namespace CarRental.CarService.Tests;

// Integrationstest: Car Service testes mod en rigtig PostgreSQL, der startes i Docker
// (Testcontainers). Det er nødvendigt, fordi reservationen er én atomisk UPDATE i databasen.
// Kræver at Docker Desktop kører.
[TestFixture]
public class CarServiceTests
{
    private PostgreSqlContainer _postgres = null!;
    private string _connectionString = null!;
    private CarSvc _sut = null!;

    // Tæller, så hver test får sin egen tomme database i containeren.
    private static int _dbCounter;

    [OneTimeSetUp]
    public async Task StartPostgres()
    {
        _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _postgres.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopPostgres()
    {
        await _postgres.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        _connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = $"car_test_{Interlocked.Increment(ref _dbCounter)}"
        }.ConnectionString;

        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();

        _sut = new CarSvc(NewContext());
    }

    private CarDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CarDbContext>().UseNpgsql(_connectionString).Options);

    private Task<Car> AddCarAsync(CarType type = CarType.Compact, int seats = 5, decimal price = 399m,
        string location = "København", CarAvailabilityStatus status = CarAvailabilityStatus.Available)
    {
        return new CarSvc(NewContext()).AddCarAsync(new Car
        {
            Make = "VW", Model = "Golf", Type = type, PassengerCapacity = seats,
            PricePerDay = price, Location = location, Status = status
        });
    }

    // ---------- Søgning ----------

    [Test]
    public async Task Search_ByLocation_IsCaseInsensitive()
    {
        var car = await AddCarAsync(location: "Roskilde");
        await AddCarAsync(location: "Aarhus");

        var result = await _sut.SearchAsync("ROSKILDE", null, null, null);

        Assert.That(result.Select(c => c.Id), Is.EqualTo(new[] { car.Id }));
    }

    [Test]
    public async Task Search_ByType_ReturnsOnlyThatType()
    {
        await AddCarAsync(CarType.SUV);
        await AddCarAsync(CarType.Economy);

        var result = await _sut.SearchAsync(null, CarType.SUV, null, null);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(CarType.SUV));
    }

    [Test]
    public async Task Search_SkipsCarsInMaintenance()
    {
        await AddCarAsync(status: CarAvailabilityStatus.InMaintenance);

        var result = await _sut.SearchAsync(null, null, null, null);

        Assert.That(result, Is.Empty);
    }

    // ---------- Tilgængelighed og reservation ----------

    [Test]
    public async Task AddCar_GetsIdFromDatabase()
    {
        var first = await AddCarAsync();
        var second = await AddCarAsync();

        Assert.That(first.Id, Is.GreaterThan(0));
        Assert.That(second.Id, Is.GreaterThan(first.Id));
    }

    [Test]
    public async Task CheckAvailability_AvailableCar_ReturnsPrice()
    {
        var car = await AddCarAsync(price: 649m);

        var result = await _sut.CheckAvailabilityAsync(new CarAvailabilityRequestDto { CarId = car.Id });

        Assert.That(result.IsAvailable, Is.True);
        Assert.That(result.PricePerDay, Is.EqualTo(649m));
    }

    [Test]
    public async Task CheckAvailability_UnknownCar_IsNotAvailable()
    {
        var result = await _sut.CheckAvailabilityAsync(new CarAvailabilityRequestDto { CarId = 999 });

        Assert.That(result.IsAvailable, Is.False);
    }

    [Test]
    public async Task TryReserve_FirstWins_SecondIsRejected()
    {
        // Krav: først-til-mølle, aldrig double booking.
        var car = await AddCarAsync();

        var first = await _sut.TryReserveAsync(car.Id);
        var second = await _sut.TryReserveAsync(car.Id);

        Assert.That(first, Is.True);
        Assert.That(second, Is.False);
    }

    [Test]
    public async Task TryReserve_TenCustomersAtTheSameTime_OnlyOneWins()
    {
        var car = await AddCarAsync();

        // 10 samtidige requests, hver med sin egen forbindelse (som 10 kunder).
        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => new CarSvc(NewContext()).TryReserveAsync(car.Id)));

        Assert.That(results.Count(r => r), Is.EqualTo(1));
    }

    [Test]
    public async Task TryReserve_CarInMaintenance_IsRejected()
    {
        var car = await AddCarAsync(status: CarAvailabilityStatus.InMaintenance);

        Assert.That(await _sut.TryReserveAsync(car.Id), Is.False);
    }

    [Test]
    public async Task Release_MakesCarBookableAgain()
    {
        var car = await AddCarAsync();
        await _sut.TryReserveAsync(car.Id);

        await _sut.ReleaseAsync(car.Id);

        Assert.That(await _sut.TryReserveAsync(car.Id), Is.True);
    }

    [Test]
    public async Task Release_CarInMaintenance_StaysInMaintenance()
    {
        var car = await AddCarAsync(status: CarAvailabilityStatus.InMaintenance);

        await _sut.ReleaseAsync(car.Id);

        var result = await _sut.CheckAvailabilityAsync(new CarAvailabilityRequestDto { CarId = car.Id });
        Assert.That(result.IsAvailable, Is.False);
    }

    // ---------- AI-anbefaling ----------

    [Test]
    public async Task Recommendations_FilterOnPassengersAndPrice_CheapestFirst()
    {
        var cheap = await AddCarAsync(seats: 7, price: 500m);
        var expensive = await AddCarAsync(seats: 9, price: 900m);
        await AddCarAsync(seats: 8, price: 2000m);   // for dyr
        await AddCarAsync(seats: 4, price: 100m);    // for få pladser

        var result = await _sut.GetAiRecommendationsAsync(passengerCount: 7, maxPricePerDay: 1000m);

        Assert.That(result.Select(c => c.Id), Is.EqualTo(new[] { cheap.Id, expensive.Id }));
    }

    // ---------- Admin: flådestyring ----------

    [Test]
    public async Task UpdateCar_ChangesPrice()
    {
        var car = await AddCarAsync(price: 399m);
        car.PricePerDay = 449m;

        var updated = await _sut.UpdateCarAsync(car);

        Assert.That(updated!.PricePerDay, Is.EqualTo(449m));
    }

    [Test]
    public async Task UpdateCar_UnknownCar_ReturnsNull()
    {
        var result = await _sut.UpdateCarAsync(new Car { Id = 999 });

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RemoveCar_RemovesIt()
    {
        var car = await AddCarAsync();

        Assert.That(await _sut.RemoveCarAsync(car.Id), Is.True);
        Assert.That(await _sut.RemoveCarAsync(car.Id), Is.False);
    }
}
