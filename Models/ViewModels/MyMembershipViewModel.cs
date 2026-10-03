using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;

namespace GameHub.Models.ViewModels
{
    public class MyMembershipViewModel
    {
        public string ArenaName { get; set; } = "GameHub Arena";
        public string CurrencySymbol { get; set; } = "Rs";
        public bool HasMembership { get; set; }
        public int? MembershipId { get; set; }
        public string? MembershipNumber { get; set; }
        public string? PlanName { get; set; }
        public string? PlanCode { get; set; }
        public string? PlanDescription { get; set; }
        public string? Benefits { get; set; }
        public string? Status { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public int RemainingDays { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int IncludedBookingHours { get; set; }
        public int UsedBookingHours { get; set; }
        public int RemainingHours { get; set; }
        public bool CanRenew { get; set; }

        /// <summary>True when at least one higher plan exists to upgrade to.</summary>
        public bool CanUpgrade { get; set; }

        public bool CanCancel { get; set; }
        public string? CancellationBlockReason { get; set; }
        public decimal TotalPaid { get; set; }
        public bool IsExpired => Status == "Expired";
        public bool IsCancelled => Status == "Cancelled";
        public decimal NextFee { get; set; }
        public DateOnly? NextExpiryDate { get; set; }
        public List<MembershipPlanCardViewModel> Plans { get; set; } = new();
        public List<MembershipHistoryItemViewModel> History { get; set; } = new();
        public int UpcomingBookingCount { get; set; }
    }

    public class MembershipPlanCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationMonths { get; set; }
        public decimal JoiningFee { get; set; }
        public decimal RenewalFee { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int IncludedBookingHours { get; set; }
        public int PriorityBookingDays { get; set; }
        public bool AllowPeakHours { get; set; }
        public bool AllowOffPeakHours { get; set; }
        public string? AllowedSportCodes { get; set; }
        public string? Benefits { get; set; }
        public bool IsCurrentPlan { get; set; }

        /// <summary>True when this plan is a genuine upgrade (higher configured fee) over the current plan.</summary>
        public bool IsUpgrade { get; set; }

        /// <summary>Amount payable to move to this plan. For a join or renew this is the full fee.</summary>
        public decimal PayableAmount { get; set; }

        /// <summary>Current plan fee, shown as the credit on an upgrade.</summary>
        public decimal CurrentPlanFee { get; set; }

        public string DurationLabel { get; set; } = string.Empty;
    }

    public class MembershipHistoryItemViewModel
    {
        public string CurrencySymbol { get; set; } = "Rs";
        public int Id { get; set; }
        public string MembershipNumber { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal JoiningFee { get; set; }
        public decimal RenewalFee { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsActive { get; set; }
        public string? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Membership this record superseded, recorded when the customer upgraded.</summary>
        public string? UpgradedFromPlan { get; set; }

        public int IncludedBookingHours { get; set; }
        public int UsedBookingHours { get; set; }
        public int? InvoiceId { get; set; }
        public string? TransactionNumber { get; set; }
        public decimal PaidAmount { get; set; }
        public string? PaymentDate { get; set; }
    }

    public class MembershipCancelViewModel
    {
        public int MembershipId { get; set; }
        public string MembershipNumber { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = "Rs";
        public DateOnly ExpiryDate { get; set; }
        public int RemainingDays { get; set; }
        public int RemainingHours { get; set; }
        public int UsedHours { get; set; }
        public int IncludedHours { get; set; }
        public decimal TotalPaid { get; set; }
        public bool AllowCustomerCancellation { get; set; }
        public string? BlockReason { get; set; }
        public string PolicySummary { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MustBeTrue(ErrorMessage = "Please confirm the cancellation to continue.")]
        public bool ConfirmCancellation { get; set; }
    }

    public class MembershipJoinViewModel
    {
        [Required]
        public int MembershipPlanId { get; set; }

        [MustBeTrue(ErrorMessage = "Please accept the membership terms.")]
        public bool AcceptMembershipTerms { get; set; }

        public string ArenaName { get; set; } = "GameHub Arena";
        public string CurrencySymbol { get; set; } = "Rs";
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string? PlanDescription { get; set; }
        public string? Benefits { get; set; }
        public int DurationMonths { get; set; }
        public int IncludedBookingHours { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsRenewal { get; set; }
        public bool IsUpgrade { get; set; }
        public int? ExistingMembershipId { get; set; }
        public string? CurrentMembershipNumber { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsSignedIn { get; set; }
        public bool RequiresEmailVerification { get; set; }
        public List<MembershipPlanCardViewModel> Plans { get; set; } = new();

        // Upgrade context
        public string? PreviousPlanName { get; set; }
        public string? PreviousPlanCode { get; set; }
        public decimal PreviousPlanFee { get; set; }
        public decimal PreviousPlanIncludedHours { get; set; }
        public int RemainingHours { get; set; }
        public int RemainingDays { get; set; }
    }
}
