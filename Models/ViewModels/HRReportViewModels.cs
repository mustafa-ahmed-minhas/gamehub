using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Filters shared by the HR Reports screen (Screen 7). Every value is sourced
    /// from live records; each report reads the subset that applies to it and
    /// simply ignores the rest, so one filter form can serve all four reports.
    /// </summary>
    public class HRReportFilterViewModel
    {
        public string Preset { get; set; } = "all";
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Department { get; set; }
        public string? Status { get; set; }

        /// <summary>Employment type, termination type, or performance issue type, depending on the report.</summary>
        public string? Category { get; set; }

        public string? Search { get; set; }
        public string SortBy { get; set; } = "newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>Shared page model for the four HR detail reports.</summary>
    public class HRReportPageViewModel
    {
        /// <summary>Lower-case key used by the export route (recruitment / employees / performance / termination).</summary>
        public string ReportKey { get; set; } = string.Empty;

        /// <summary>MVC action name used to rebuild this report's links.</summary>
        public string ActionName { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-bar-chart-line";
        public DateTime RefreshedAt { get; set; }
        public DateOnly RangeStart { get; set; }
        public DateOnly RangeEnd { get; set; }
        public HRReportFilterViewModel Filter { get; set; } = new();
        public IReadOnlyList<string> Departments { get; set; } = Array.Empty<string>();
        public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = Array.Empty<SelectListItem>();
        public string StatusLabel { get; set; } = "Status";
        public string CategoryLabel { get; set; } = "Category";
        public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = Array.Empty<ReportKpiViewModel>();
        public IReadOnlyList<ReportChartViewModel> Charts { get; set; } = Array.Empty<ReportChartViewModel>();
        public IReadOnlyList<ReportInsightViewModel> Insights { get; set; } = Array.Empty<ReportInsightViewModel>();
        public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
        public IReadOnlyList<HRReportTableRowViewModel> Rows { get; set; } = Array.Empty<HRReportTableRowViewModel>();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; } = 1;
        public string FilterSummary { get; set; } = string.Empty;
        public bool HasFilters { get; set; }
    }

    /// <summary>Aggregate landing page for HR Reports (Screen 7).</summary>
    public class HRReportDashboardViewModel
    {
        public DateTime RefreshedAt { get; set; }
        public IReadOnlyList<ReportKpiViewModel> Kpis { get; set; } = Array.Empty<ReportKpiViewModel>();
        public IReadOnlyList<ReportChartViewModel> Charts { get; set; } = Array.Empty<ReportChartViewModel>();
        public IReadOnlyList<ReportInsightViewModel> Insights { get; set; } = Array.Empty<ReportInsightViewModel>();
        public IReadOnlyList<ReportModuleLinkViewModel> Modules { get; set; } = Array.Empty<ReportModuleLinkViewModel>();
    }

    /// <summary>
    /// A single row of an HR report table. Mirrors the shared
    /// <see cref="ReportTableRowViewModel"/> but additionally carries the HR
    /// badge tone so the source screens' status colours are preserved.
    /// </summary>
    public class HRReportTableRowViewModel
    {
        public IReadOnlyList<string> Cells { get; set; } = Array.Empty<string>();
        public string? Status { get; set; }
        public string? Tone { get; set; }
        public string? DetailUrl { get; set; }
    }
}
