namespace GameHub.Models.ViewModels
{
    public class AvailableSlotViewModel
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int? PricingRuleId { get; set; }
        public decimal BasePrice { get; set; }
        public bool IsPeakRate { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>Slot length (minutes) derived from the court's schedule.</summary>
        public int DurationMinutes { get; set; }
    }
}
