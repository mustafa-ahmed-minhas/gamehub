using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerMembershipDetailsViewModel : CustomerMembershipListItemViewModel
    {
        public string CustomerCode { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public MembershipPlanType PlanType { get; set; }
        public decimal JoiningFee { get; set; }
        public decimal RenewalFee { get; set; }
        public string? AllowedSportCodes { get; set; }
        public string? Benefits { get; set; }
        public DateOnly? FrozenFrom { get; set; }
        public DateOnly? FrozenUntil { get; set; }
        public DateOnly? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CurrencySymbol { get; set; } = "PKR";
        public string ArenaName { get; set; } = "GameHub Arena";
        public bool CanManage { get; set; }
    }
}
