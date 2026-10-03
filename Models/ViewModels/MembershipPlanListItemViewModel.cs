using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class MembershipPlanListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public MembershipPlanType PlanType { get; set; }
        public int DurationMonths { get; set; }
        public decimal JoiningFee { get; set; }
        public decimal RenewalFee { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int IncludedBookingHours { get; set; }
        public string? AllowedSportCodes { get; set; }
        public int MemberCount { get; set; }
        public bool IsActive { get; set; }
    }
}
