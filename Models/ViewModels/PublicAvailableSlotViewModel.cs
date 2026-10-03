namespace GameHub.Models.ViewModels
{
    public class PublicAvailableSlotViewModel
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string DisplayTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public decimal BasePrice { get; set; }
        public string FormattedPrice { get; set; } = string.Empty;
        public bool IsPeakRate { get; set; }
        public string PricingLabel { get; set; } = string.Empty;
        public string? UnavailableReason { get; set; }
    }
}
