using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class ArenaSetupViewModel
    {
        public string ActiveTab { get; set; } = "sports";
        public string? Search { get; set; }
        public string? StatusFilter { get; set; }
        public string? SortBy { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; } = 1;
        public int ActiveSports { get; set; }
        public int ActiveFacilities { get; set; }
        public int ActiveCourts { get; set; }
        public int PricingRules { get; set; }
        public int WeeklySchedules { get; set; }
        public bool CanManage { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public string? Day { get; set; }
        public string? Type { get; set; }
        public string? OperationalStatus { get; set; }
        public IReadOnlyList<SportListItemViewModel> Sports { get; set; } = Array.Empty<SportListItemViewModel>();
        public IReadOnlyList<FacilityListItemViewModel> Facilities { get; set; } = Array.Empty<FacilityListItemViewModel>();
        public IReadOnlyList<CourtListItemViewModel> Courts { get; set; } = Array.Empty<CourtListItemViewModel>();
        public IReadOnlyList<PricingListItemViewModel> Pricings { get; set; } = Array.Empty<PricingListItemViewModel>();
        public IReadOnlyList<ScheduleListItemViewModel> Schedules { get; set; } = Array.Empty<ScheduleListItemViewModel>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> FacilityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class SportListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? IconClass { get; set; }
        public string? AccentColor { get; set; }
        public int DefaultDurationMinutes { get; set; }
        public int DisplayOrder { get; set; }
        public int CourtCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class FacilityListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public FacilityType Type { get; set; }
        public string? LocationLabel { get; set; }
        public int CourtCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class CourtListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public CourtStatus Status { get; set; }
        public int Capacity { get; set; }
        public int PricingCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class PricingListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CourtName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public bool IsPeakRate { get; set; }
        public bool IsActive { get; set; }
    }

    public class ScheduleListItemViewModel
    {
        public int Id { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpeningTime { get; set; }
        public TimeOnly ClosingTime { get; set; }
        public bool IsClosed { get; set; }
        public int SlotDurationMinutes { get; set; }
        public int BufferMinutes { get; set; }
        public bool IsActive { get; set; }
    }
}
