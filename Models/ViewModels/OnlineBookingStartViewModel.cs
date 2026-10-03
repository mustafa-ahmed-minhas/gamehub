namespace GameHub.Models.ViewModels
{
    public class OnlineBookingStartViewModel
    {
        public int CourtId { get; set; }
        public int SportId { get; set; }
        public string ArenaName { get; set; } = "GameHub Arena";
        public string CurrencySymbol { get; set; } = "Rs";
        public string CourtName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string SportCode { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string FacilityType { get; set; } = string.Empty;
        public string? CourtImageUrl { get; set; }
        public string? Description { get; set; }
        public int Capacity { get; set; }
        public decimal? StartingPrice { get; set; }
        public int DefaultDurationMinutes { get; set; }
        public DateOnly MinDate { get; set; }
        public DateOnly MaxDate { get; set; }
        public DateOnly Tomorrow { get; set; }
        public DateOnly WeekendDate { get; set; }
        public IReadOnlyList<int> DurationOptions { get; set; } = Array.Empty<int>();
        public IReadOnlyList<string> OperatingSummary { get; set; } = Array.Empty<string>();
        public bool IsCustomerSignedIn { get; set; }
        public bool EmailVerified { get; set; }
    }
}
