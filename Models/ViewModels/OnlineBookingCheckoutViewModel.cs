namespace GameHub.Models.ViewModels
{
    public class OnlineBookingCheckoutViewModel
    {
        public bool IsExpired { get; set; }
        public string CheckoutToken { get; set; } = string.Empty;
        public string ArenaName { get; set; } = "GameHub Arena";
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public int CourtId { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = "Rs";
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PlayerCount { get; set; }
        public string? MembershipSummary { get; set; }
        public string? CustomerNotes { get; set; }
        public string? SpecialRequest { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPeakRate { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public int SecondsRemaining { get; set; }
    }
}
