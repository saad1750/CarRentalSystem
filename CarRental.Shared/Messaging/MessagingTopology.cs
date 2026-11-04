namespace CarRental.Shared.Messaging;

// Fælles navne for RabbitMQ, så publisher og consumer altid er enige.
//
// Alle events sendes til ét topic exchange. Hver service, der vil lytte,
// opretter sin EGEN kø og binder den til de routing keys, den er interesseret i.
// Så kan nye services abonnere på et event uden at afsenderen skal ændres.
public static class MessagingTopology
{
    // Exchange som alle events publiceres til.
    public const string EventsExchange = "carrental.events";

    // Beskeder der fejler i en consumer ender her i stedet for at forsvinde.
    public const string DeadLetterExchange = "carrental.events.dlx";
    public const string DeadLetterQueue = "carrental.dead-letter";

    public static class RoutingKeys
    {
        public const string BookingConfirmed = "booking.confirmed";
        public const string PaymentSucceeded = "payment.succeeded";
        public const string PaymentFailed = "payment.failed";
    }
}
