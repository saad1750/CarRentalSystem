using CarRental.CarService.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.CarService.Data;

// Car Service ejer sin egen database (cardb).
public class CarDbContext : DbContext
{
    public CarDbContext(DbContextOptions<CarDbContext> options) : base(options) { }

    public DbSet<Car> Cars => Set<Car>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Car>(car =>
        {
            car.ToTable("cars");
            car.HasKey(c => c.Id);

            car.Property(c => c.Make).HasMaxLength(100).IsRequired();
            car.Property(c => c.Model).HasMaxLength(100).IsRequired();
            car.Property(c => c.Location).HasMaxLength(100).IsRequired();
            car.Property(c => c.PricePerDay).HasPrecision(10, 2);

            // Enums gemmes som tekst, så tabellen kan læses direkte i databasen.
            car.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
            car.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

            // xmin som concurrency token (se Car.Version).
            car.Property(c => c.Version).IsRowVersion();

            // Søgning filtrerer på status, type og lokation.
            car.HasIndex(c => new { c.Status, c.Type });
            car.HasIndex(c => c.Location);
        });
    }
}
