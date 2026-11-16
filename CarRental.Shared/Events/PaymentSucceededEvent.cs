namespace CarRental.Shared.Events;

// Sendes fra Payment Service når en betaling er godkendt.
// Booking Service lytter og bekræfter bookingen.
public class PaymentSucceededEvent
{
    public int BookingId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
}
