using CarRental.PaymentService.Models;
using CarRental.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.PaymentService.Data;

// Testdata: én betaling pr. booking i Booking Service' testdata (betaling 1 hører til booking 1 osv.).
public static class PaymentSeedData
{
    public static async Task SeedAsync(PaymentDbContext db)
    {
        if (await db.Payments.AnyAsync())
            return;

        db.Payments.AddRange(
            Payment(1, booking: 1, 837m, PaymentStatus.Succeeded, Utc(2026, 8, 20, 12, 1)),
            Payment(2, booking: 2, 4543m, PaymentStatus.Succeeded, Utc(2026, 8, 28, 15, 31)),
            Payment(3, booking: 3, 1698m, PaymentStatus.Succeeded, Utc(2026, 9, 25, 9, 46)),
            Payment(4, booking: 4, 2198m, PaymentStatus.Refunded, Utc(2026, 10, 15, 11, 1), processed: Utc(2026, 10, 25, 14, 31)),  // annulleret gratis
            Payment(5, booking: 5, 2274m, PaymentStatus.Succeeded, Utc(2026, 11, 1, 18, 21)),
            Payment(6, booking: 6, 1798m, PaymentStatus.Refunded, Utc(2026, 11, 2, 16, 41), processed: Utc(2026, 11, 9, 18, 1)),     // refunderet minus 25 % gebyr
            Payment(7, booking: 7, 1077m, PaymentStatus.Succeeded, Utc(2026, 11, 10, 8, 11)),
            Payment(8, booking: 8, 5243m, PaymentStatus.Succeeded, Utc(2026, 11, 12, 20, 6)),
            Payment(9, booking: 9, 249m, PaymentStatus.Failed, Utc(2026, 11, 15, 10, 0), reason: "Card declined"),
            Payment(10, booking: 10, 1947m, PaymentStatus.Failed, Utc(2026, 11, 15, 13, 20), reason: "Insufficient funds"));

        await db.SaveChangesAsync();
        await db.ResetIdSequenceAsync("payments");
    }

    private static Payment Payment(int id, int booking, decimal amount, PaymentStatus status, DateTime created,
        DateTime? processed = null, string? reason = null) => new()
    {
        Id = id, BookingId = booking, Amount = amount, Status = status, CreatedAt = created,
        ProcessedAt = processed ?? created.AddSeconds(2), FailureReason = reason
    };

    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);
}
