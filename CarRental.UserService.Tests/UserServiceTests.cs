using CarRental.UserService.Data;
using CarRental.UserService.Models;
using Microsoft.EntityFrameworkCore;
using UserSvc = CarRental.UserService.Services.UserService;

namespace CarRental.UserService.Tests;

[TestFixture]
public class UserServiceTests
{
    private UserSvc _sut = null!;

    // Tæller, så hver test får sin egen database i hukommelsen.
    private static int _dbCounter;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase($"user-test-{Interlocked.Increment(ref _dbCounter)}")
            .Options;

        _sut = new UserSvc(new UserDbContext(options));
    }

    private static Customer NewCustomer(string email, DateTime dateOfBirth, DateTime licenceExpiry, string licence = "DK-12345678") => new()
    {
        FullName = "Test Kunde",
        Email = email,
        DateOfBirth = dateOfBirth,
        DriversLicenceNumber = licence,
        DriversLicenceExpiry = licenceExpiry
    };

    // ---------- Registrering og login ----------

    [Test]
    public async Task Register_NewEmail_GetsIdAndHashedPassword()
    {
        var customer = NewCustomer("anna@test.dk", new DateTime(1995, 1, 1), DateTime.UtcNow.AddYears(5));

        var result = await _sut.RegisterCustomerAsync(customer, "Secret123!");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.GreaterThan(0));
        Assert.That(result.PasswordHash, Is.Not.EqualTo("Secret123!"));
    }

    [Test]
    public async Task Register_EmailAlreadyUsed_ReturnsNull()
    {
        await _sut.RegisterCustomerAsync(NewCustomer("anna@test.dk", new DateTime(1995, 1, 1), DateTime.UtcNow.AddYears(5)), "pw");

        var second = await _sut.RegisterCustomerAsync(NewCustomer("anna@test.dk", new DateTime(1990, 1, 1), DateTime.UtcNow.AddYears(5)), "pw");

        Assert.That(second, Is.Null);
    }

    [Test]
    public async Task Login_CorrectPassword_ReturnsUser()
    {
        await _sut.RegisterCustomerAsync(NewCustomer("anna@test.dk", new DateTime(1995, 1, 1), DateTime.UtcNow.AddYears(5)), "Hemmelig1!");

        var user = await _sut.LoginAsync("anna@test.dk", "Hemmelig1!");

        Assert.That(user, Is.Not.Null);
    }

    [Test]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        await _sut.RegisterCustomerAsync(NewCustomer("anna@test.dk", new DateTime(1995, 1, 1), DateTime.UtcNow.AddYears(5)), "Hemmelig1!");

        var user = await _sut.LoginAsync("anna@test.dk", "forkert");

        Assert.That(user, Is.Null);
    }

    // ---------- Eligibility: 21+ og gyldigt kørekort ----------

    [Test]
    public async Task Eligibility_AdultWithValidLicence_IsEligible()
    {
        var customer = await _sut.RegisterCustomerAsync(
            NewCustomer("anna@test.dk", new DateTime(1995, 5, 10), DateTime.UtcNow.AddYears(5)), "pw");

        var result = await _sut.CheckEligibilityAsync(customer!.Id);

        Assert.That(result.IsEligible, Is.True);
    }

    [Test]
    public async Task Eligibility_Under21_IsRejected()
    {
        var customer = await _sut.RegisterCustomerAsync(
            NewCustomer("mads@test.dk", DateTime.UtcNow.AddYears(-19), DateTime.UtcNow.AddYears(5)), "pw");

        var result = await _sut.CheckEligibilityAsync(customer!.Id);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reason, Is.EqualTo("Customer is under 21 years old"));
    }

    [Test]
    public async Task Eligibility_ExpiredLicence_IsRejected()
    {
        var customer = await _sut.RegisterCustomerAsync(
            NewCustomer("sara@test.dk", new DateTime(1988, 1, 1), DateTime.UtcNow.AddDays(-1)), "pw");

        var result = await _sut.CheckEligibilityAsync(customer!.Id);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reason, Is.EqualTo("Customer does not have a valid driver's licence"));
    }

    [Test]
    public async Task Eligibility_UnknownCustomer_IsRejected()
    {
        var result = await _sut.CheckEligibilityAsync(999);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reason, Is.EqualTo("Customer not found"));
    }

    // ---------- Customer-modellen ----------

    [Test]
    public void Customer_Exactly21Today_IsEligible()
    {
        var customer = NewCustomer("x", DateTime.UtcNow.Date.AddYears(-21), DateTime.UtcNow.AddYears(1));

        Assert.That(customer.Age, Is.EqualTo(21));
        Assert.That(customer.IsEligibleToRent, Is.True);
    }

    [Test]
    public void Customer_Turns21Tomorrow_IsNotEligible()
    {
        var customer = NewCustomer("x", DateTime.UtcNow.Date.AddYears(-21).AddDays(1), DateTime.UtcNow.AddYears(1));

        Assert.That(customer.Age, Is.EqualTo(20));
        Assert.That(customer.IsEligibleToRent, Is.False);
    }

    [Test]
    public void Customer_NoLicenceNumber_IsNotEligible()
    {
        var customer = NewCustomer("x", new DateTime(1990, 1, 1), DateTime.UtcNow.AddYears(1), licence: "");

        Assert.That(customer.HasValidLicence, Is.False);
        Assert.That(customer.IsEligibleToRent, Is.False);
    }
}
