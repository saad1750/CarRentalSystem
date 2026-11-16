using CarRental.PaymentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.PaymentService.Data;

// Payment Service ejer sin egen database (paymentdb).
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("payments");
            payment.HasKey(p => p.Id);

            payment.Property(p => p.Amount).HasPrecision(10, 2);
            payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            payment.Property(p => p.FailureReason).HasMaxLength(500);

            payment.HasIndex(p => p.BookingId);

            // Krav: gentagne betalinger må ikke give dubletter. Databasen tillader højst
            // én vellykket betaling pr. booking – også hvis to forespørgsler kommer samtidig.
            // (Fejlede forsøg må der gerne være flere af.)
            payment.HasIndex(p => p.BookingId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Succeeded'")
                .HasDatabaseName("IX_payments_BookingId_succeeded");
        });
    }
}
