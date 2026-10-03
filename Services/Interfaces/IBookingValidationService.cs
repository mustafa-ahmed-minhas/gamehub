using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Services.Interfaces
{
    public interface IBookingValidationService
    {
        Task<BookingAvailabilityResult> CheckAvailabilityAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId = null);

        Task<BookingValidationResult> ValidateBookingAsync(
            int customerId, int sportId, int facilityId, int courtId,
            DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime,
            int durationMinutes, int playerCount, decimal manualDiscountAmount,
            BookingSource source, bool isWalkIn, int? excludeBookingId = null);

        Task<BookingValidationResult> ValidateRescheduleAsync(
            int bookingId, int courtId, DateOnly bookingDate,
            TimeOnly startTime, TimeOnly endTime, int durationMinutes);

        Task<bool> HasActiveConflictAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end, int? excludeBookingId = null);
    }

    public class BookingAvailabilityResult
    {
        public bool IsAvailable { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool CourtValid { get; set; }
        public bool ScheduleValid { get; set; }
        public bool WithinHours { get; set; }
        public bool NoConflict { get; set; }
        public string? ConflictDetail { get; set; }
    }

    public class BookingValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public BookingAvailabilityResult? Availability { get; set; }
    }
}
