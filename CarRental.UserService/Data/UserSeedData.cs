using CarRental.UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.UserService.Data;

// Testdata til udvikling og integrationstest. Faste id'er, så de andre services'
// testdata (fx bookinger) kan pege på de samme kunder. Se DOCKER.md for oversigt.
// Alle brugere har kodeordet "Test123!".
public static class UserSeedData
{
    public const string Password = "Test123!";

    public static readonly Guid AnnaId = Guid.Parse("11111111-1111-1111-1111-000000000001");      // må leje
    public static readonly Guid MikkelId = Guid.Parse("11111111-1111-1111-1111-000000000002");    // under 21
    public static readonly Guid SaraId = Guid.Parse("11111111-1111-1111-1111-000000000003");      // udløbet kørekort
    public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-000000000100");
    public static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-000000000200");

    public static async Task SeedAsync(UserDbContext db)
    {
        if (await db.Users.AnyAsync())
            return; // allerede seedet – køres aldrig to gange

        var hash = Services.UserService.HashPassword(Password);

        db.Customers.AddRange(
            new Customer
            {
                Id = AnnaId, FullName = "Anna Jensen", Email = "anna@test.dk", PasswordHash = hash,
                DateOfBirth = new DateTime(1995, 4, 12), DriversLicenceNumber = "DK-10293847",
                DriversLicenceExpiry = new DateTime(2032, 6, 30)
            },
            new Customer
            {
                Id = MikkelId, FullName = "Mikkel Larsen", Email = "mikkel@test.dk", PasswordHash = hash,
                DateOfBirth = new DateTime(2008, 3, 1), DriversLicenceNumber = "DK-55519283",
                DriversLicenceExpiry = new DateTime(2035, 3, 1)
            },
            new Customer
            {
                Id = SaraId, FullName = "Sara Nielsen", Email = "sara@test.dk", PasswordHash = hash,
                DateOfBirth = new DateTime(1988, 11, 23), DriversLicenceNumber = "DK-33384756",
                DriversLicenceExpiry = new DateTime(2024, 1, 31)
            });

        db.Admins.Add(new Admin
        {
            Id = AdminId, FullName = "Admin Bruger", Email = "admin@carrental.dk", PasswordHash = hash
        });

        db.Employees.Add(new Employee
        {
            Id = EmployeeId, FullName = "Jonas Medarbejder", Email = "employee@carrental.dk", PasswordHash = hash,
            AssignedLocation = "København"
        });

        await db.SaveChangesAsync();
    }
}
