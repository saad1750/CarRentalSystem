using CarRental.NotificationService.Services;
using CarRental.Shared.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationSvc = CarRental.NotificationService.Services.NotificationService;

namespace CarRental.NotificationService.Tests;

// NB: NotificationService venter 2 sekunder mellem hvert nyt forsøg,
// så testene med fejlende afsendere tager et par sekunder.
[TestFixture]
public class NotificationServiceTests
{
    private static readonly BookingConfirmedEvent Event = new()
    {
        BookingId = 1,
        CustomerId = 1,
        CustomerEmail = "lars@test.dk",
        CarId = 1,
        StartDate = new DateTime(2026, 12, 1),
        EndDate = new DateTime(2026, 12, 4),
        TotalPrice = 837m
    };

    private static NotificationSvc CreateSut(params INotificationSender[] senders) =>
        new(senders, NullLogger<NotificationSvc>.Instance);

    [Test]
    public async Task SendsOnEveryChannel()
    {
        var email = new ScriptedSender("Email", true);
        var sms = new ScriptedSender("SMS", true);

        await CreateSut(email, sms).NotifyBookingConfirmedAsync(Event);

        Assert.That(email.Calls, Is.EqualTo(1));
        Assert.That(sms.Calls, Is.EqualTo(1));
        Assert.That(email.LastRecipient, Is.EqualTo("lars@test.dk"));
    }

    [Test]
    public async Task FailsTwiceThenSucceeds_RetriesUntilSuccess()
    {
        // Krav: fejlet afsendelse prøves igen.
        var email = new ScriptedSender("Email", false, false, true);

        await CreateSut(email).NotifyBookingConfirmedAsync(Event);

        Assert.That(email.Calls, Is.EqualTo(3));
    }

    [Test]
    public void AlwaysFails_StopsAfter3AttemptsAndDoesNotThrow()
    {
        // Krav: en fejlende notifikation må aldrig ramme bookingen.
        var email = new ScriptedSender("Email", false);

        Assert.DoesNotThrowAsync(() => CreateSut(email).NotifyBookingConfirmedAsync(Event));
        Assert.That(email.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task OneChannelFails_OtherChannelStillSends()
    {
        var sms = new ScriptedSender("SMS", false);
        var email = new ScriptedSender("Email", true);

        await CreateSut(sms, email).NotifyBookingConfirmedAsync(Event);

        Assert.That(email.Calls, Is.EqualTo(1));
    }

    // Falsk afsender: returnerer resultaterne i rækkefølge (den sidste gentages).
    private sealed class ScriptedSender : INotificationSender
    {
        private readonly bool[] _results;

        public ScriptedSender(string channel, params bool[] results)
        {
            Channel = channel;
            _results = results;
        }

        public string Channel { get; }
        public int Calls { get; private set; }
        public string? LastRecipient { get; private set; }

        public Task<bool> SendAsync(string recipient, string subject, string body)
        {
            LastRecipient = recipient;
            var result = _results[Math.Min(Calls, _results.Length - 1)];
            Calls++;
            return Task.FromResult(result);
        }
    }
}
