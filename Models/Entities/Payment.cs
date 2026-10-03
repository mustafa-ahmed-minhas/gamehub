using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string PaymentNumber { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public DateOnly PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public InvoicePaymentMethod PaymentMethod { get; set; }
        [MaxLength(120)] public string? ReferenceNumber { get; set; }
        [MaxLength(1500)] public string? Notes { get; set; }
        public int ReceivedByUserId { get; set; }
        public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Recorded;
        [Required, MaxLength(20)] public string Currency { get; set; } = "PKR";
        [MaxLength(8)] public string CurrencySymbol { get; set; } = "Rs";
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        [Timestamp] public byte[]? RowVersion { get; set; }

        public SalesInvoice Invoice { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
        public User ReceivedByUser { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
        public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
    }
}
