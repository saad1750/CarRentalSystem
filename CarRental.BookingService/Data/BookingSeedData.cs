using CarRental.BookingService.Models;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.BookingService.Data;

// Testdata: 10 bookinger i kronologisk rækkefølge (id 1-10) med alle statusser.
// CustomerId/CarId matcher testdata i User Service (kunde 1-10) og Car Service (bil 1-10).
// Pris = antal dage x pris pr. dag. Se DOCKER.md.
public static class BookingSeedData
{
    public static async Task SeedAsync(BookingDbContext db)
    {
        if (await db.Bookings.AnyAsync())
            return;

        db.Bookings.AddRange(
            // Afsluttede lejeperioder
            Booking(1, customer: 1, car: 1, new(2026, 9, 5, 10, 0, 0), new(2026, 9, 8, 10, 0, 0), 279m, BookingStatus.Completed, Utc(2026, 8, 20, 12, 0)),   // 3 dage =   837
            Booking(2, customer: 2, car: 6, new(2026, 9, 12, 9, 0, 0), new(2026, 9, 19, 9, 0, 0), 649m, BookingStatus.Completed, Utc(2026, 8, 28, 15, 30)),  // 7 dage = 4.543
            Booking(3, customer: 3, car: 9, new(2026, 10, 2, 8, 0, 0), new(2026, 10, 4, 8, 0, 0), 849m, BookingStatus.Completed, Utc(2026, 9, 25, 9, 45)),   // 2 dage = 1.698
            // Annulleret mere end 48 timer før: gratis
            Booking(4, customer: 7, car: 7, new(2026, 11, 5, 10, 0, 0), new(2026, 11, 7, 10, 0, 0), 1099m, BookingStatus.CancelledFree, Utc(2026, 10, 15, 11, 0),
                cancelledAt: Utc(2026, 10, 25, 14, 30), fee: 0m),                                                                                              // 2 dage = 2.198
            // Bilen er hentet og ude hos kunden nu
            Booking(5, customer: 4, car: 3, new(2026, 11, 14, 10, 0, 0), new(2026, 11, 20, 10, 0, 0), 379m, BookingStatus.PickedUp, Utc(2026, 11, 1, 18, 20)), // 6 dage = 2.274
            // Annulleret mindre end 48 timer før: 25 % gebyr
            Booking(6, customer: 8, car: 10, new(2026, 11, 10, 10, 0, 0), new(2026, 11, 12, 10, 0, 0), 899m, BookingStatus.CancelledWithFee, Utc(2026, 11, 2, 16, 40),
                cancelledAt: Utc(2026, 11, 9, 18, 0), fee: 449.50m),                                                                                           // 2 dage = 1.798
            // Bekræftede kommende bookinger
            Booking(7, customer: 5, car: 4, new(2026, 11, 28, 12, 0, 0), new(2026, 12, 1, 12, 0, 0), 359m, BookingStatus.Confirmed, Utc(2026, 11, 10, 8, 10)), // 3 dage = 1.077
            Booking(8, customer: 6, car: 5, new(2026, 12, 20, 9, 0, 0), new(2026, 12, 27, 9, 0, 0), 749m, BookingStatus.Confirmed, Utc(2026, 11, 12, 20, 5)),  // 7 dage = 5.243
            // Betalingen fejlede: bookingen er annulleret og bilen frigivet
            Booking(9, customer: 1, car: 2, new(2026, 12, 5, 10, 0, 0), new(2026, 12, 6, 10, 0, 0), 249m, BookingStatus.Failed, Utc(2026, 11, 15, 10, 0),
                cancelledAt: Utc(2026, 11, 15, 10, 0)),                                                                                                        // 1 dag  =   249
            Booking(10, customer: 3, car: 6, new(2026, 12, 10, 10, 0, 0), new(2026, 12, 13, 10, 0, 0), 649m, BookingStatus.Failed, Utc(2026, 11, 15, 13, 20),
                cancelledAt: Utc(2026, 11, 15, 13, 20)));                                                                                                      // 3 dage = 1.947

        await db.SaveChangesAsync();
        await db.ResetIdSequenceAsync("bookings");
    }

    private static Booking Booking(int id, int customer, int car, DateTime start, DateTime end, decimal pricePerDay,
        BookingStatus status, DateTime created, DateTime? cancelledAt = null, decimal? fee = null) => new()
    {
        Id = id, CustomerId = customer, CarId = car, StartDate = start, EndDate = end, PricePerDay = pricePerDay,
        Status = status, CreatedAt = created, CancelledAt = cancelledAt, CancellationFee = fee
    };

    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);
}
