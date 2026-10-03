using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class BookingListItemViewModel
    {
        public int Id { get; set; }
        public string BookingNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerInitials { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public bool IsMemberBooking { get; set; }
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public BookingStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public BookingSource Source { get; set; }
        public bool IsWalkIn { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
