using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class ReportFilterViewModel
    {
        public string Preset { get; set; } = "this-month";
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? CustomerId { get; set; }
        public int? OwnerId { get; set; }
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string SortBy { get; set; } = "newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class ReportFilterOptionsViewModel
    {
        public IReadOnlyList<SelectListItem> Sports { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> Facilities { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> Courts { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> Customers { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> Owners { get; set; } = Array.Empty<SelectListItem>();
    }

    public class ReportPageViewModel
    {
        public string ReportKey { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Currency { get; set; } = "PKR";
        public string CurrencySymbol { get; set; } = "Rs";
        public string ArenaName { get; set; } = "GameHub Arena";
        public string TimeZoneLabel { get; set; } = "Pakistan Standard Time";
        public DateTime RefreshedAt { get; set; }
        public DateOnly RangeStart { get; set; }
        public DateOnly RangeEnd { get; set; }
        public ReportFilterViewModel Filter { get; set; } = new();
        public ReportFilterOptionsViewModel Options { get; set; } = new();
        public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = Array.Empty<ReportKpiViewModel>();
        public IReadOnlyList<ReportChartViewModel> Charts { get; set; } = Array.Empty<ReportChartViewModel>();
        public IReadOnlyList<ReportInsightViewModel> Insights { get; set; } = Array.Empty<ReportInsightViewModel>();
        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ReportTableRowViewModel> Rows { get; set; } = Array.Empty<ReportTableRowViewModel>();
        public IReadOnlyList<ReportModuleLinkViewModel> Modules { get; set; } = Array.Empty<ReportModuleLinkViewModel>();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; } = 1;
        public bool ShowFinancials { get; set; }
        public bool IsOverview { get; set; }
        public string FilterSummary { get; set; } = string.Empty;
    }

    public class ReportKpiViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Helper { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-bar-chart";
        public string Tone { get; set; } = string.Empty;
    }

    public class ReportChartViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Type { get; set; } = "bar";
        public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<decimal> Values { get; set; } = Array.Empty<decimal>();
        public IReadOnlyList<string> Colors { get; set; } = Array.Empty<string>();
        public bool Currency { get; set; }
    }

    public class ReportInsightViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-lightbulb";
        public string Tone { get; set; } = string.Empty;
    }

    public class ReportTableRowViewModel
    {
        public IReadOnlyList<string> Cells { get; set; } = Array.Empty<string>();
        public string? Status { get; set; }
        public string? DetailUrl { get; set; }
    }

    public class ReportModuleLinkViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-graph-up";
        public bool Available { get; set; } = true;
    }
}
