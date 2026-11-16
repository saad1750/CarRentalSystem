using CarRental.UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.UserService.Data;

// Testdata til udvikling og integrationstest. Id'erne er faste (1-12) og i
// kronologisk rækkefølge, så Booking Service' testdata kan pege på de samme kunder.
// Alle brugere har kodeordet "Test123!". Se DOCKER.md for oversigt.
public static class UserSeedData
{
    public const string Password = "Test123!";

    public static async Task SeedAsync(UserDbContext db)
    {
        if (await db.Users.AnyAsync())
            return; // allerede seedet – køres aldrig to gange

        var hash = Services.UserService.HashPassword(Password);

        db.Customers.AddRange(
            Customer(1, "Lars Pedersen", "lars@test.dk", new(1985, 2, 14), "DK-20481937", new(2031, 2, 14), new(2026, 8, 1, 9, 12, 0)),
            Customer(2, "Mette Hansen", "mette@test.dk", new(1979, 7, 3), "DK-30572846", new(2029, 7, 3), new(2026, 8, 3, 14, 40, 0)),
            Customer(3, "Ahmed Khan", "ahmed@test.dk", new(1992, 9, 21), "DK-40663755", new(2033, 9, 21), new(2026, 8, 10, 18, 5, 0)),
            Customer(4, "Sofie Andersen", "sofie@test.dk", new(1998, 1, 30), "DK-50754664", new(2034, 1, 30), new(2026, 8, 15, 8, 30, 0)),
            Customer(5, "Rasmus Kristensen", "rasmus@test.dk", new(1990, 5, 11), "DK-60845573", new(2030, 5, 11), new(2026, 9, 1, 11, 20, 0)),
            Customer(6, "Fatima Ali", "fatima@test.dk", new(1994, 12, 2), "DK-70936482", new(2032, 12, 2), new(2026, 9, 7, 16, 45, 0)),
            Customer(7, "Jonas Møller", "jonas@test.dk", new(1983, 8, 19), "DK-81027391", new(2028, 8, 19), new(2026, 9, 20, 10, 0, 0)),
            Customer(8, "Ida Christensen", "ida@test.dk", new(2001, 3, 27), "DK-91118200", new(2035, 3, 27), new(2026, 10, 2, 13, 15, 0)),
            // Afvises ved booking: under 21
            Customer(9, "Emil Poulsen", "emil@test.dk", new(2007, 6, 15), "DK-12209119", new(2036, 6, 15), new(2026, 10, 11, 19, 30, 0)),
            // Afvises ved booking: udløbet kørekort
            Customer(10, "Nanna Holm", "nanna@test.dk", new(1975, 10, 8), "DK-23300028", new(2025, 5, 31), new(2026, 10, 20, 7, 50, 0)));

        db.Admins.Add(new Admin
        {
            Id = 11, FullName = "Admin Bruger", Email = "admin@carrental.dk", PasswordHash = hash,
            CreatedAt = Utc(2026, 10, 25, 9, 0)
        });

        db.Employees.Add(new Employee
        {
            Id = 12, FullName = "Jonas Medarbejder", Email = "employee@carrental.dk", PasswordHash = hash,
            AssignedLocation = "København", CreatedAt = Utc(2026, 10, 26, 9, 0)
        });

        await db.SaveChangesAsync();
        await db.ResetIdSequenceAsync("users");

        Customer Customer(int id, string name, string email, DateTime born, string licence, DateTime licenceExpiry, DateTime created) => new()
        {
            Id = id, FullName = name, Email = email, PasswordHash = hash,
            DateOfBirth = born, DriversLicenceNumber = licence, DriversLicenceExpiry = licenceExpiry,
            CreatedAt = DateTime.SpecifyKind(created, DateTimeKind.Utc)
        };
    }

    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);
}
