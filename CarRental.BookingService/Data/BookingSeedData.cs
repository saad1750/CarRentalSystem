using CarRental.BookingService.Models;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.BookingService.Data;

// Testdata: booking-historik for testkunden Anna. Id'erne matcher testdata i
// User Service (kunder) og Car Service (biler). Kun afsluttede/annullerede
// bookinger, så alle biler stadig er ledige. Se DOCKER.md.
public static class BookingSeedData
{
    private static readonly Guid AnnaId = Guid.Parse("11111111-1111-1111-1111-000000000001");
    private static readonly Guid VwGolfId = Guid.Parse("22222222-2222-2222-2222-000000000002");
    private static readonly Guid KiaSportageId = Guid.Parse("22222222-2222-2222-2222-000000000004");

    public static readonly Guid CompletedBookingId = Guid.Parse("33333333-3333-3333-3333-000000000001");
    public static readonly Guid CancelledBookingId = Guid.Parse("33333333-3333-3333-3333-000000000002");

    public static async Task SeedAsync(BookingDbContext db)
    {
        if (await db.Bookings.AnyAsync())
            return;

        db.Bookings.AddRange(
            // 3 dage x 399 kr = 1.197 kr (matcher betalingen i Payment Service' testdata)
            new Booking
            {
                Id = CompletedBookingId, CustomerId = AnnaId, CarId = VwGolfId,
                StartDate = new DateTime(2026, 10, 1, 10, 0, 0), EndDate = new DateTime(2026, 10, 4, 10, 0, 0),
                PricePerDay = 399m, Status = BookingStatus.Completed,
                CreatedAt = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc)
            },
            new Booking
            {
                Id = CancelledBookingId, CustomerId = AnnaId, CarId = KiaSportageId,
                StartDate = new DateTime(2026, 10, 15, 9, 0, 0), EndDate = new DateTime(2026, 10, 17, 9, 0, 0),
                PricePerDay = 599m, Status = BookingStatus.CancelledFree, CancellationFee = 0m,
                CreatedAt = new DateTime(2026, 10, 2, 8, 30, 0, DateTimeKind.Utc),
                CancelledAt = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc)
            });

        await db.SaveChangesAsync();
    }
}
