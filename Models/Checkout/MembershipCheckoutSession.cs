namespace GameHub.Models.Checkout
{
    public enum MembershipOperation
    {
        Join = 0,
        Renew = 1,
        Upgrade = 2
    }

    public class MembershipCheckoutSession
    {
        public string CheckoutToken { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int MembershipPlanId { get; set; }
        public int? ExistingMembershipId { get; set; }
        public bool IsRenewal { get; set; }
        public MembershipOperation Operation { get; set; }
        public string ArenaName { get; set; } = "GameHub Arena";
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanDescription { get; set; }
        public string? Benefits { get; set; }
        public int DurationMonths { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int IncludedBookingHours { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";

        /// <summary>Plan the customer is moving away from on an upgrade. Null for join and renew.</summary>
        public string? PreviousPlanName { get; set; }

        public string? PreviousPlanCode { get; set; }

        /// <summary>Joining fee of the previous plan, used to compute the upgrade price difference.</summary>
        public decimal PreviousPlanFee { get; set; }

        public decimal FeeAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}
