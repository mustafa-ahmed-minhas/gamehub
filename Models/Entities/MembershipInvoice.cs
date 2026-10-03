using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class MembershipInvoice
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public int CustomerMembershipId { get; set; }
        public int CustomerId { get; set; }
        public int? PaymentTransactionId { get; set; }
        [Required, MaxLength(80)]
        public string CheckoutToken { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }

        [Required, MaxLength(100)]
        public string PlanName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string PlanCode { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? PlanDescription { get; set; }

        /// <summary>Which membership operation this invoice bills: Join, Renew or Upgrade.</summary>
        [MaxLength(20)]
        public string? Operation { get; set; }

        /// <summary>Plan the customer moved away from on an upgrade invoice.</summary>
        [MaxLength(100)]
        public string? PreviousPlanName { get; set; }

        [MaxLength(20)]
        public string? PreviousPlanCode { get; set; }

        public int DurationMonths { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }

        [Required, MaxLength(20)]
        public string Currency { get; set; } = string.Empty;

        [MaxLength(8)]
        public string? CurrencySymbol { get; set; }

        [Required, MaxLength(120)]
        public string ArenaName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? ArenaPhone { get; set; }

        [MaxLength(150)]
        public string? ArenaEmail { get; set; }

        [MaxLength(250)]
        public string? ArenaAddress { get; set; }

        [Required, MaxLength(120)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? CustomerEmail { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [Required, MaxLength(30)]
        public string MembershipNumber { get; set; } = string.Empty;

        public DateOnly StartDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; }

        public CustomerMembership CustomerMembership { get; set; } = default!;
        public Customer Customer { get; set; } = default!;
        public PaymentTransaction? PaymentTransaction { get; set; }
    }
}
