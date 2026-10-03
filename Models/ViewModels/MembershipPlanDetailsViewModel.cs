using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class MembershipPlanDetailsViewModel : MembershipPlanListItemViewModel
    {
        public string? Description { get; set; }
        public int PriorityBookingDays { get; set; }
        public bool AllowPeakHours { get; set; }
        public bool AllowOffPeakHours { get; set; }
        public string? Benefits { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CurrencySymbol { get; set; } = "PKR";
        public string ArenaName { get; set; } = "GameHub Arena";
        public bool CanManage { get; set; }
        public IReadOnlyList<CustomerMembershipListItemViewModel> AssignedMembers { get; set; } = Array.Empty<CustomerMembershipListItemViewModel>();
    }
}
