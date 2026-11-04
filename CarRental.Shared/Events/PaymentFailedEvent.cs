namespace CarRental.Shared.Events;

// Krav: hvis betaling fejler, annulleres reservationen (saga-kompensation).
// Sendes fra Payment Service via RabbitMQ; Booking Service lytter og
// annullerer bookingen + frigiver bilen.
public class PaymentFailedEvent
{
    public Guid BookingId { get; set; }
    public Guid PaymentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
