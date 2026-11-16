using CarRental.PaymentService.Data;
using CarRental.PaymentService.Services;
using CarRental.Shared.Enums;
using CarRental.Shared.Events;
using Microsoft.EntityFrameworkCore;
using PaymentSvc = CarRental.PaymentService.Services.PaymentService;

namespace CarRental.PaymentService.Tests;

[TestFixture]
public class PaymentServiceTests
{
    private FakePaymentEventPublisher _events = null!;
    private PaymentSvc _sut = null!;

    // Tæller, så hver test får sin egen database i hukommelsen.
    private static int _dbCounter;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase($"payment-test-{Interlocked.Increment(ref _dbCounter)}")
            .Options;

        _events = new FakePaymentEventPublisher();
        _sut = new PaymentSvc(new PaymentDbContext(options), _events);
    }

    [Test]
    public async Task ProcessPayment_Succeeds_PublishesPaymentSucceededEvent()
    {
        var payment = await _sut.ProcessPaymentAsync(bookingId: 1, amount: 1197m);

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Succeeded));
        Assert.That(payment.Id, Is.GreaterThan(0));
        Assert.That(_events.Succeeded, Has.Count.EqualTo(1));
        Assert.That(_events.Succeeded[0].BookingId, Is.EqualTo(1));
        Assert.That(_events.Succeeded[0].PaymentId, Is.EqualTo(payment.Id));
        Assert.That(_events.Succeeded[0].Amount, Is.EqualTo(1197m));
        Assert.That(_events.Failed, Is.Empty);
    }

    [Test]
    public async Task ProcessPayment_SameBookingTwice_ChargesOnlyOnce()
    {
        // Krav: samme booking betales aldrig to gange.
        var first = await _sut.ProcessPaymentAsync(bookingId: 1, amount: 500m);
        var second = await _sut.ProcessPaymentAsync(bookingId: 1, amount: 500m);

        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(_events.Succeeded, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ProcessPayment_TwoDifferentBookings_GetTwoPayments()
    {
        var first = await _sut.ProcessPaymentAsync(bookingId: 1, amount: 500m);
        var second = await _sut.ProcessPaymentAsync(bookingId: 2, amount: 500m);

        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(_events.Succeeded, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ProcessPayment_SetsProcessedAt()
    {
        var payment = await _sut.ProcessPaymentAsync(bookingId: 1, amount: 250m);

        Assert.That(payment.ProcessedAt, Is.Not.Null);
    }

    [Test]
    public async Task GetByBookingId_ReturnsThePayment()
    {
        var payment = await _sut.ProcessPaymentAsync(bookingId: 3, amount: 250m);

        var found = await _sut.GetByBookingIdAsync(3);

        Assert.That(found!.Id, Is.EqualTo(payment.Id));
    }

    [Test]
    public async Task GetByBookingId_Unknown_ReturnsNull()
    {
        Assert.That(await _sut.GetByBookingIdAsync(999), Is.Null);
    }
}

// Falsk udgave af RabbitMQ-publisheren, så testen ikke kræver RabbitMQ.
public class FakePaymentEventPublisher : IPaymentEventPublisher
{
    public List<PaymentSucceededEvent> Succeeded { get; } = new();
    public List<PaymentFailedEvent> Failed { get; } = new();

    public Task PublishPaymentSucceededAsync(PaymentSucceededEvent paymentSucceeded)
    {
        Succeeded.Add(paymentSucceeded);
        return Task.CompletedTask;
    }

    public Task PublishPaymentFailedAsync(PaymentFailedEvent paymentFailed)
    {
        Failed.Add(paymentFailed);
        return Task.CompletedTask;
    }
}
