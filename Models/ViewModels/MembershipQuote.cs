using GameHub.Models.Checkout;
using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Centralised membership pricing/eligibility result. Both the customer UI and the payment
    /// finalizer consume this so the amount a customer is shown is exactly the amount charged.
    /// </summary>
    public class MembershipQuote
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public MembershipOperation Operation { get; set; }
        public MembershipPlan? Plan { get; set; }
        public CustomerMembership? Current { get; set; }

        public decimal FeeAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }

        public string? PreviousPlanName { get; set; }
        public string? PreviousPlanCode { get; set; }
        public decimal PreviousPlanFee { get; set; }
        public int RemainingHours { get; set; }

        /// <summary>
        /// Included hours the new term should carry. On an upgrade this never drops below the hours
        /// the customer has left, so an upgrade can never take away already-paid time.
        /// </summary>
        public int IncludedHoursAfterOperation { get; set; }
    }
}
