using System.Security.Cryptography;
using System.Text;
using CarRental.Shared.DTOs;
using CarRental.UserService.Data;
using CarRental.UserService.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.UserService.Services;

// Gemmer brugere i PostgreSQL via EF Core (UserDbContext).
public class UserService : IUserService
{
    private readonly UserDbContext _db;

    public UserService(UserDbContext db)
    {
        _db = db;
    }

    // Krav: registrering.
    public async Task<Customer?> RegisterCustomerAsync(Customer customer, string plainTextPassword)
    {
        if (await _db.Users.AnyAsync(u => u.Email == customer.Email))
            return null; // email allerede i brug

        customer.PasswordHash = HashPassword(plainTextPassword);
        _db.Customers.Add(customer);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // To samtidige registreringer med samme email: det unikke index i databasen
            // afviser den ene, selvom begge nåede forbi tjekket ovenfor.
            return null;
        }

        return customer;
    }

    // Krav: login – én tabel for alle roller, så ét opslag er nok.
    public async Task<User?> LoginAsync(string email, string plainTextPassword)
    {
        var hash = HashPassword(plainTextPassword);
        return await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hash);
    }

    public Task<Customer?> GetCustomerByIdAsync(Guid customerId) =>
        _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);

    public Task<List<Customer>> GetAllCustomersAsync() =>
        _db.Customers.AsNoTracking().OrderBy(c => c.FullName).ToListAsync();

    // Krav: eligibility-tjek som Booking Service kalder før booking oprettes.
    public async Task<CustomerEligibilityDto> CheckEligibilityAsync(Guid customerId)
    {
        var customer = await GetCustomerByIdAsync(customerId);

        if (customer is null)
        {
            return new CustomerEligibilityDto
            {
                CustomerId = customerId,
                IsEligible = false,
                Reason = "Customer not found"
            };
        }

        if (!customer.IsEligibleToRent)
        {
            var reason = customer.Age < 21
                ? "Customer is under 21 years old"
                : "Customer does not have a valid driver's licence";

            return new CustomerEligibilityDto
            {
                CustomerId = customerId,
                IsEligible = false,
                Reason = reason
            };
        }

        return new CustomerEligibilityDto
        {
            CustomerId = customerId,
            IsEligible = true
        };
    }

    // Midlertidig simpel hashing — skift til ASP.NET Identity/BCrypt før produktion.
    // internal, så testdata kan bruge samme hashing.
    internal static string HashPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
