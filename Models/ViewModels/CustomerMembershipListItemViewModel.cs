using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerMembershipListItemViewModel
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerInitials { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string MembershipNumber { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public int RemainingDays { get; set; }
        public int IncludedBookingHours { get; set; }
        public int UsedBookingHours { get; set; }
        public decimal DiscountPercentage { get; set; }
        public MembershipStatus Status { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsActive { get; set; }
    }
}
