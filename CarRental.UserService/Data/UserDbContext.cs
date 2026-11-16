using CarRental.Shared.Enums;
using CarRental.UserService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarRental.UserService.Data;

// User Service ejer sin egen database (userdb) – ingen anden service tilgår den direkte.
public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Employee> Employees => Set<Employee>();

    // Datoer uden klokkeslæt (fødselsdato, kørekortets udløb) gemmes som "date".
    // Kind fjernes, så både "1995-01-01" og "1995-01-01T00:00:00Z" kan gemmes.
    private static readonly ValueConverter<DateTime, DateTime> DateOnlyConverter =
        new(v => DateTime.SpecifyKind(v.Date, DateTimeKind.Unspecified), v => v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.Id);

            // Én tabel for alle tre brugertyper (TPH). Role er discriminator,
            // så EF ved om en række er Customer, Admin eller Employee.
            user.HasDiscriminator(u => u.Role)
                .HasValue<Customer>(UserRole.Customer)
                .HasValue<Admin>(UserRole.Admin)
                .HasValue<Employee>(UserRole.Employee);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            user.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            user.Property(u => u.Email).HasMaxLength(320).IsRequired();
            user.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();

            // Krav: email bruges som login og skal være unik – håndhæves også af databasen.
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.Property(c => c.DateOfBirth).HasColumnType("date").HasConversion(DateOnlyConverter);
            customer.Property(c => c.DriversLicenceExpiry).HasColumnType("date").HasConversion(DateOnlyConverter);
            customer.Property(c => c.DriversLicenceNumber).HasMaxLength(50);
            // Age, HasValidLicence og IsEligibleToRent beregnes og gemmes ikke.
        });

        modelBuilder.Entity<Employee>().Property(e => e.AssignedLocation).HasMaxLength(100);
    }
}
