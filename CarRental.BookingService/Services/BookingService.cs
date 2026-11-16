using CarRental.BookingService.Data;
using CarRental.BookingService.Models;
using CarRental.Shared.Enums;
using CarRental.Shared.Events;
using Microsoft.EntityFrameworkCore;

namespace CarRental.BookingService.Services;

// Gemmer bookinger i PostgreSQL via EF Core (BookingDbContext).
public class BookingService : IBookingService
{
    private readonly BookingDbContext _db;
    private readonly ICarServiceClient _carServiceClient;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IEventPublisher _eventPublisher;

    public BookingService(
        BookingDbContext db,
        ICarServiceClient carServiceClient,
        IUserServiceClient userServiceClient,
        IEventPublisher eventPublisher)
    {
        _db = db;
        _carServiceClient = carServiceClient;
        _userServiceClient = userServiceClient;
        _eventPublisher = eventPublisher;
    }

    // Krav: booking af bil — tjekker duplikat, eligibility, tilgængelighed,
    // og reserverer bilen, i den rækkefølge, før noget gemmes.
    public async Task<BookingResult> CreateBookingAsync(int customerId, int carId, DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
            return new BookingResult(false, "End date must be after start date.", null);

        // Krav: tjek om booking allerede findes.
        if (await HasOverlappingBookingAsync(customerId, carId, startDate, endDate))
            return new BookingResult(false, "An overlapping booking already exists for this car.", null);

        // Krav: 21+ og gyldigt kørekort.
        var eligibility = await _userServiceClient.CheckEligibilityAsync(customerId);
        if (eligibility is null || !eligibility.IsEligible)
            return new BookingResult(false, eligibility?.Reason ?? "Customer is not eligible to rent.", null);

        // Krav: realtidstjek af tilgængelighed.
        var availability = await _carServiceClient.CheckAvailabilityAsync(carId, startDate, endDate);
        if (availability is null || !availability.IsAvailable)
            return new BookingResult(false, "Car is not available for the selected period.", null);

        // Krav: først-til-mølle, aldrig double booking — atomisk hos Car Service.
        var reserved = await _carServiceClient.TryReserveAsync(carId);
        if (!reserved)
            return new BookingResult(false, "Car was just reserved by another customer.", null);

        var booking = new Booking
        {
            CustomerId = customerId,
            CarId = carId,
            StartDate = startDate,
            EndDate = endDate,
            PricePerDay = availability.PricePerDay,
            Status = BookingStatus.Pending
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return new BookingResult(true, null, booking);
    }

    // Krav: tjek om booking allerede findes (overlap på samme bil).
    public Task<bool> HasOverlappingBookingAsync(int customerId, int carId, DateTime startDate, DateTime endDate)
    {
        return _db.Bookings.AnyAsync(b =>
            b.CarId == carId &&
            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PickedUp) &&
            startDate < b.EndDate && endDate > b.StartDate);
    }

    // Krav: booking-historik.
    public Task<List<Booking>> GetBookingHistoryAsync(int customerId)
    {
        return _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    // Kaldes efter vellykket betaling.
    // Idempotent: RabbitMQ leverer "mindst én gang", så samme payment.succeeded kan komme
    // to gange. En allerede bekræftet booking bekræftes ikke igen (og sender ikke en ny mail).
    public async Task<BookingResult> ConfirmBookingAsync(int bookingId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking is null)
            return new BookingResult(false, "Booking not found.", null);

        if (booking.Status == BookingStatus.Confirmed)
            return new BookingResult(true, null, booking);

        if (booking.Status != BookingStatus.Pending)
            return new BookingResult(false, $"Booking cannot be confirmed in status {booking.Status}.", null);

        booking.Status = BookingStatus.Confirmed;
        await _db.SaveChangesAsync();

        // Krav: bekræftelse sendes asynkront, Notification Service-fejl må ikke ramme bookingen.
        await _eventPublisher.PublishBookingConfirmedAsync(new BookingConfirmedEvent
        {
            BookingId = booking.Id,
            CustomerId = booking.CustomerId,
            CarId = booking.CarId,
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            TotalPrice = booking.TotalPrice
        });

        return new BookingResult(true, null, booking);
    }

    // Krav: fejlet betaling annullerer reservationen.
    // Idempotent: kun en ventende booking kan fejle, så bilen frigives højst én gang.
    public async Task HandlePaymentFailedAsync(int bookingId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking is null || booking.Status != BookingStatus.Pending)
            return;

        booking.Status = BookingStatus.Failed;
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _carServiceClient.ReleaseAsync(booking.CarId);
    }

    // Krav: gratis annullering op til 48 timer før, ellers 25% gebyr,
    // og ingen annullering efter afhentning.
    public async Task<BookingResult> CancelBookingAsync(int bookingId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking is null)
            return new BookingResult(false, "Booking not found.", null);

        if (!booking.CanBeCancelled)
            return new BookingResult(false, "Booking cannot be cancelled after pickup.", null);

        var now = DateTime.UtcNow;

        if (booking.IsWithinFreeCancellationWindow(now))
        {
            booking.Status = BookingStatus.CancelledFree;
            booking.CancellationFee = 0m;
        }
        else
        {
            booking.Status = BookingStatus.CancelledWithFee;
            booking.CancellationFee = booking.TotalPrice * 0.25m;
        }

        booking.CancelledAt = now;
        await _db.SaveChangesAsync();

        await _carServiceClient.ReleaseAsync(booking.CarId);

        return new BookingResult(true, null, booking);
    }

    // Personale: bekræft afhentning.
    public async Task<BookingResult> ConfirmPickupAsync(int bookingId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking is null)
            return new BookingResult(false, "Booking not found.", null);

        if (booking.Status != BookingStatus.Confirmed)
            return new BookingResult(false, "Only a confirmed booking can be picked up.", null);

        booking.Status = BookingStatus.PickedUp;
        await _db.SaveChangesAsync();
        return new BookingResult(true, null, booking);
    }

    // Personale: registrer aflevering.
    public async Task<BookingResult> RegisterReturnAsync(int bookingId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking is null)
            return new BookingResult(false, "Booking not found.", null);

        if (booking.Status != BookingStatus.PickedUp)
            return new BookingResult(false, "Only a picked-up booking can be returned.", null);

        booking.Status = BookingStatus.Completed;
        await _db.SaveChangesAsync();
        await _carServiceClient.ReleaseAsync(booking.CarId);

        return new BookingResult(true, null, booking);
    }
}
