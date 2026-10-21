using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class BookingApprovalsViewModel
    {
        public IReadOnlyList<BookingApprovalListItemViewModel> Items { get; set; } = Array.Empty<BookingApprovalListItemViewModel>();
        public string? Search { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? CustomerId { get; set; }
        public DateOnly? DateFrom { get; set; }
        public DateOnly? DateTo { get; set; }
        public string SortBy { get; set; } = "date_asc";
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public bool CanManage { get; set; }
        public List<SelectListItem> Sports { get; set; } = new();
        public List<SelectListItem> Facilities { get; set; } = new();
        public List<SelectListItem> Courts { get; set; } = new();
        public List<SelectListItem> Customers { get; set; } = new();
        
        // Pre-computed properties for simplified view
        public int ActiveFilterCount { get; set; }
        public int StartRecord { get; set; }
        public int EndRecord { get; set; }
        public int PageStart { get; set; }
        public int PageEnd { get; set; }
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> SortOptions { get; set; } = new();
        public List<SelectListItem> PageSizeOptions { get; set; } = new();
        public List<PageLink> PaginationLinks { get; set; } = new();
        public Dictionary<string, string> CurrentQueryValues { get; set; } = new();
    }

    public class PageLink
    {
        public int PageNumber { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDisabled { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}