namespace CarRental.Shared.Events;

// Sendes fra Payment Service når en betaling er godkendt.
// Booking Service lytter og bekræfter bookingen.
public class PaymentSucceededEvent
{
    public Guid BookingId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
}
