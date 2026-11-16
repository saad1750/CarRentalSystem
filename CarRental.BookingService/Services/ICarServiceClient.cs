using CarRental.Shared.DTOs;

namespace CarRental.BookingService.Services;

// Kapsler de synkrone REST-kald til Car Service.
public interface ICarServiceClient
{
    Task<CarAvailabilityResponseDto?> CheckAvailabilityAsync(int carId, DateTime start, DateTime end);
    Task<bool> TryReserveAsync(int carId);
    Task ReleaseAsync(int carId);
}
