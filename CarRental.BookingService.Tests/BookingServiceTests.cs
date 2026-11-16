using CarRental.BookingService.Data;
using CarRental.BookingService.Services;
using CarRental.Shared.DTOs;
using CarRental.Shared.Enums;
using CarRental.Shared.Events;
using Microsoft.EntityFrameworkCore;
using BookingSvc = CarRental.BookingService.Services.BookingService;

namespace CarRental.BookingService.Tests;

[TestFixture]
public class BookingServiceTests
{
    private FakeCarServiceClient _cars = null!;
    private FakeUserServiceClient _users = null!;
    private FakeEventPublisher _events = null!;
    private BookingSvc _sut = null!;

    private const int CustomerId = 1;
    private const int CarId = 1;

    // Tæller, så hver test får sin egen database i hukommelsen.
    private static int _dbCounter;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase($"booking-test-{Interlocked.Increment(ref _dbCounter)}")
            .Options;

        _cars = new FakeCarServiceClient();
        _users = new FakeUserServiceClient();
        _events = new FakeEventPublisher();
        _sut = new BookingSvc(new BookingDbContext(options), _cars, _users, _events);
    }

    // ---------- Oprettelse ----------

    [Test]
    public async Task CreateBooking_ValidRequest_IsPendingAndReservesCar()
    {
        var result = await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(13));

        Assert.That(result.Success, Is.True);
        Assert.That(result.Booking!.Id, Is.GreaterThan(0));
        Assert.That(result.Booking.Status, Is.EqualTo(BookingStatus.Pending));
        Assert.That(result.Booking.TotalPrice, Is.EqualTo(1200m)); // 3 dage x 400
        Assert.That(_cars.Reserved, Does.Contain(CarId));
    }

    [Test]
    public async Task CreateBooking_EndBeforeStart_IsRejected()
    {
        var result = await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(3));

        Assert.That(result.Success, Is.False);
        Assert.That(_cars.Reserved, Is.Empty);
    }

    [Test]
    public async Task CreateBooking_CustomerUnder21_IsRejected()
    {
        _users.IsEligible = false;
        _users.Reason = "Customer is under 21 years old";

        var result = await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(12));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo("Customer is under 21 years old"));
        Assert.That(_cars.Reserved, Is.Empty);
    }

    [Test]
    public async Task CreateBooking_CarAlreadyReserved_IsRejected()
    {
        _cars.ReserveSucceeds = false; // en anden kunde nåede det først

        var result = await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(12));

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task CreateBooking_OverlapsExistingBooking_IsRejected()
    {
        await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(15));

        var result = await _sut.CreateBookingAsync(2, CarId, DateTime.UtcNow.AddDays(12), DateTime.UtcNow.AddDays(18));

        Assert.That(result.Success, Is.False);
    }

    // ---------- Annullering ----------

    [Test]
    public async Task Cancel_MoreThan48HoursBefore_IsFree()
    {
        var booking = (await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(7))).Booking!;

        var result = await _sut.CancelBookingAsync(booking.Id);

        Assert.That(result.Booking!.Status, Is.EqualTo(BookingStatus.CancelledFree));
        Assert.That(result.Booking.CancellationFee, Is.EqualTo(0m));
        Assert.That(_cars.Released, Does.Contain(CarId));
    }

    [Test]
    public async Task Cancel_LessThan48HoursBefore_Costs25Percent()
    {
        var start = DateTime.UtcNow.AddHours(24);
        var booking = (await _sut.CreateBookingAsync(CustomerId, CarId, start, start.AddDays(4))).Booking!;

        var result = await _sut.CancelBookingAsync(booking.Id);

        Assert.That(result.Booking!.Status, Is.EqualTo(BookingStatus.CancelledWithFee));
        Assert.That(result.Booking.CancellationFee, Is.EqualTo(400m)); // 25% af 4 x 400
    }

    [Test]
    public async Task Cancel_AfterPickup_IsRejected()
    {
        var booking = (await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(7))).Booking!;
        await _sut.ConfirmBookingAsync(booking.Id);
        await _sut.ConfirmPickupAsync(booking.Id);

        var result = await _sut.CancelBookingAsync(booking.Id);

        Assert.That(result.Success, Is.False);
    }

    // ---------- Betaling (via RabbitMQ) ----------

    [Test]
    public async Task PaymentFailed_BookingFailsAndCarIsReleased()
    {
        var booking = (await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(12))).Booking!;

        await _sut.HandlePaymentFailedAsync(booking.Id);

        Assert.That(booking.Status, Is.EqualTo(BookingStatus.Failed));
        Assert.That(_cars.Released, Does.Contain(CarId));
    }

    [Test]
    public async Task ConfirmBooking_PublishesBookingConfirmedEvent()
    {
        var booking = (await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(12))).Booking!;

        await _sut.ConfirmBookingAsync(booking.Id);

        Assert.That(booking.Status, Is.EqualTo(BookingStatus.Confirmed));
        Assert.That(_events.Published, Has.Count.EqualTo(1));
        Assert.That(_events.Published[0].BookingId, Is.EqualTo(booking.Id));
    }

    [Test]
    public async Task ConfirmBooking_SameMessageTwice_PublishesOnlyOnce()
    {
        // RabbitMQ kan levere samme besked to gange.
        var booking = (await _sut.CreateBookingAsync(CustomerId, CarId, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(12))).Booking!;

        await _sut.ConfirmBookingAsync(booking.Id);
        await _sut.ConfirmBookingAsync(booking.Id);

        Assert.That(_events.Published, Has.Count.EqualTo(1));
    }
}

// ---------- Fakes: falske udgaver af Car Service, User Service og RabbitMQ ----------
// Så testes Booking Service alene, uden at de andre services skal køre.

public class FakeCarServiceClient : ICarServiceClient
{
    public bool ReserveSucceeds { get; set; } = true;
    public List<int> Reserved { get; } = new();
    public List<int> Released { get; } = new();

    public Task<CarAvailabilityResponseDto?> CheckAvailabilityAsync(int carId, DateTime start, DateTime end) =>
        Task.FromResult<CarAvailabilityResponseDto?>(new CarAvailabilityResponseDto
        {
            CarId = carId, IsAvailable = true, PricePerDay = 400m
        });

    public Task<bool> TryReserveAsync(int carId)
    {
        if (ReserveSucceeds) Reserved.Add(carId);
        return Task.FromResult(ReserveSucceeds);
    }

    public Task ReleaseAsync(int carId)
    {
        Released.Add(carId);
        return Task.CompletedTask;
    }
}

public class FakeUserServiceClient : IUserServiceClient
{
    public bool IsEligible { get; set; } = true;
    public string? Reason { get; set; }

    public Task<CustomerEligibilityDto?> CheckEligibilityAsync(int customerId) =>
        Task.FromResult<CustomerEligibilityDto?>(new CustomerEligibilityDto
        {
            CustomerId = customerId, IsEligible = IsEligible, Reason = Reason
        });
}

public class FakeEventPublisher : IEventPublisher
{
    public List<BookingConfirmedEvent> Published { get; } = new();

    public Task PublishBookingConfirmedAsync(BookingConfirmedEvent bookingConfirmed)
    {
        Published.Add(bookingConfirmed);
        return Task.CompletedTask;
    }
}
