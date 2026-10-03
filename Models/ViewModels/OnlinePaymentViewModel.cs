using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class OnlinePaymentViewModel
    {
        [Required]
        public string CheckoutToken { get; set; } = string.Empty;

        [Required]
        public OnlinePaymentMethod PaymentMethod { get; set; }

        public string? CardholderName { get; set; }
        public string? CardNumber { get; set; }
        public string? ExpiryMonth { get; set; }
        public string? ExpiryYear { get; set; }
        public string? Cvv { get; set; }
        public string? WalletProvider { get; set; }
        public string? WalletNumber { get; set; }
        public bool SimulateBankTransferSuccess { get; set; }

        [MustBeTrue(ErrorMessage = "Please accept the payment terms.")]
        public bool AcceptPaymentTerms { get; set; }

        public string ArenaName { get; set; } = "GameHub Arena";
        public string CustomerName { get; set; } = string.Empty;
        public string CourtName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PlayerCount { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public decimal BaseAmount { get; set; }
        public decimal MembershipDiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int SecondsRemaining { get; set; }
        public bool IsDevelopmentDemo { get; set; }

        public bool IsMembershipPayment { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanBenefits { get; set; }
        public int DurationMonths { get; set; }
        public int IncludedBookingHours { get; set; }
        public DateOnly MembershipStartDate { get; set; }
        public DateOnly MembershipExpiryDate { get; set; }
        public bool IsRenewal { get; set; }

        /// <summary>Join, Renew or Upgrade, used to label the membership payment screen.</summary>
        public string MembershipOperation { get; set; } = "Join";

        /// <summary>Plan the customer is moving away from, shown only on an upgrade.</summary>
        public string? PreviousPlanName { get; set; }
    }
}
