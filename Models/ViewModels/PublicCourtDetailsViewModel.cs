using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class PublicCourtDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string FacilityType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int Capacity { get; set; }
        public CourtStatus Status { get; set; }
        public int DefaultDurationMinutes { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public IReadOnlyList<PublicPricingItemViewModel> Pricings { get; set; } = Array.Empty<PublicPricingItemViewModel>();
        public IReadOnlyList<PublicScheduleItemViewModel> Schedules { get; set; } = Array.Empty<PublicScheduleItemViewModel>();
        public string SupportPhone { get; set; } = string.Empty;
        public string SupportEmail { get; set; } = string.Empty;
    }

    public class PublicPricingItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Price { get; set; }
        public bool IsPeakRate { get; set; }
    }

    public class PublicScheduleItemViewModel
    {
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpeningTime { get; set; }
        public TimeOnly ClosingTime { get; set; }
        public int SlotDurationMinutes { get; set; }
        public int BufferMinutes { get; set; }
    }
}
