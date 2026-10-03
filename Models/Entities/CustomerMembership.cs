using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class CustomerMembership
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string MembershipNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }
        public int MembershipPlanId { get; set; }
        public int? PaymentTransactionId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public MembershipStatus Status { get; set; }
        public decimal JoiningFee { get; set; }
        public decimal RenewalFee { get; set; }
        public decimal DiscountPercentage { get; set; }
        public int IncludedBookingHours { get; set; }
        public int UsedBookingHours { get; set; }
        public DateOnly? FrozenFrom { get; set; }
        public DateOnly? FrozenUntil { get; set; }
        public DateOnly? CancelledAt { get; set; }

        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Membership record that replaced this one when the customer renewed or upgraded. Set only
        /// when this record is closed by a later operation, so history stays traceable.
        /// </summary>
        public int? SupersededByMembershipId { get; set; }

        public bool AutoRenew { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Customer Customer { get; set; } = default!;
        public MembershipPlan MembershipPlan { get; set; } = default!;
        public PaymentTransaction? PaymentTransaction { get; set; }
        public CustomerMembership? SupersededByMembership { get; set; }
    }
}
