using CarRental.PaymentService.Models;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.PaymentService.Data;

// Testdata: betalingen for testkunden Annas afsluttede booking i Booking Service.
public static class PaymentSeedData
{
    private static readonly Guid CompletedBookingId = Guid.Parse("33333333-3333-3333-3333-000000000001");
    public static readonly Guid CompletedPaymentId = Guid.Parse("44444444-4444-4444-4444-000000000001");

    public static async Task SeedAsync(PaymentDbContext db)
    {
        if (await db.Payments.AnyAsync())
            return;

        db.Payments.Add(new Payment
        {
            Id = CompletedPaymentId,
            BookingId = CompletedBookingId,
            Amount = 1197m,
            Status = PaymentStatus.Succeeded,
            CreatedAt = new DateTime(2026, 9, 20, 12, 1, 0, DateTimeKind.Utc),
            ProcessedAt = new DateTime(2026, 9, 20, 12, 1, 2, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();
    }
}
