namespace GameHub.Models.ViewModels
{
    public class BookingPriceSummaryViewModel
    {
        public decimal BaseAmount { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal ManualDiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public bool IsPeakRate { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public string Message { get; set; } = string.Empty;

        /// <summary>Set when no usable pricing rule exists for the selected court and slot.</summary>
        public string? PricingError { get; set; }

        /// <summary>Set when the submitted manual discount had to be adjusted or rejected.</summary>
        public string? ManualDiscountMessage { get; set; }

        /// <summary>Set when the submitted paid amount had to be adjusted or rejected.</summary>
        public string? PaidAmountMessage { get; set; }
    }
}
