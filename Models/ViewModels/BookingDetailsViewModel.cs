using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class BookingDetailsViewModel : BookingListItemViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string CustomerType { get; set; } = string.Empty;
        public int LoyaltyPoints { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int PlayerCount { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal ManualDiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? CustomerNotes { get; set; }
        public string? InternalNotes { get; set; }
        public string? CancellationReason { get; set; }
        public string? RescheduleReason { get; set; }
        public string? SpecialRequest { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? MembershipNumber { get; set; }
        public string? MembershipPlanName { get; set; }
        public bool MembershipHoursApplied { get; set; }
        public string CreatedByName { get; set; } = "System";
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public bool CanManage { get; set; }
        public bool CanOperate { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
    }
}
