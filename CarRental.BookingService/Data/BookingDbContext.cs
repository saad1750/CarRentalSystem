using CarRental.BookingService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarRental.BookingService.Data;

// Booking Service ejer sin egen database (bookingdb).
public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<Booking> Bookings => Set<Booking>();

    // Afhentning/aflevering er lokale tidspunkter på den ene udlejningslokation, så de
    // gemmes som "timestamp without time zone". Kind fjernes, så både
    // "2026-11-20T10:00:00" og "2026-11-20T10:00:00Z" kan gemmes.
    private static readonly ValueConverter<DateTime, DateTime> LocalTimeConverter =
        new(v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified), v => v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(booking =>
        {
            booking.ToTable("bookings");
            booking.HasKey(b => b.Id);

            booking.Property(b => b.StartDate).HasColumnType("timestamp without time zone").HasConversion(LocalTimeConverter);
            booking.Property(b => b.EndDate).HasColumnType("timestamp without time zone").HasConversion(LocalTimeConverter);

            booking.Property(b => b.PricePerDay).HasPrecision(10, 2);
            booking.Property(b => b.CancellationFee).HasPrecision(10, 2);
            booking.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

            // CustomerId og CarId er id'er fra andre services' databaser –
            // ingen foreign keys på tværs af services, kun indexes til opslag.
            booking.HasIndex(b => b.CustomerId);
            booking.HasIndex(b => new { b.CarId, b.Status });

            // DurationInDays, TotalPrice og CanBeCancelled beregnes og gemmes ikke.
        });
    }
}
