using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class BookingApprovalListItemViewModel
    {
        public int Id { get; set; }
        public string BookingNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string SportName { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string CourtName { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PlayerCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public int? PaymentTransactionId { get; set; }
        public string? PaymentReference { get; set; }
        public string? GatewayReference { get; set; }
        public string? GatewayTransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}