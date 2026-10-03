using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class MembershipIndexViewModel
    {
        public string ActiveTab { get; set; } = "plans";
        public string? Search { get; set; }
        public string? StatusFilter { get; set; }
        public string? SortBy { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public MembershipPlanType? PlanType { get; set; }
        public MembershipStatus? MembershipStatus { get; set; }
        public int? PlanId { get; set; }
        public string? SportCode { get; set; }
        public string? ExpiryState { get; set; }
        public int ActivePlans { get; set; }
        public int ActiveMembers { get; set; }
        public int ExpiringSoon { get; set; }
        public int FrozenMemberships { get; set; }
        public int ExpiredMemberships { get; set; }
        public decimal EstimatedValue { get; set; }
        public string CurrencySymbol { get; set; } = "PKR";
        public bool CanManage { get; set; }
        public IReadOnlyList<MembershipPlanListItemViewModel> Plans { get; set; } = Array.Empty<MembershipPlanListItemViewModel>();
        public IReadOnlyList<CustomerMembershipListItemViewModel> CustomerMemberships { get; set; } = Array.Empty<CustomerMembershipListItemViewModel>();
        public IReadOnlyList<SelectListItem> PlanOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
    }
}
