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
    }
}
