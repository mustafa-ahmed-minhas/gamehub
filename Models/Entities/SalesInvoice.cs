using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class SalesInvoice
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string InvoiceNumber { get; set; } = string.Empty;
        public InvoiceSourceType SourceType { get; set; }
        public int? SalesOrderId { get; set; }
        public int? SalesQuotationId { get; set; }
        public int CustomerId { get; set; }
        public int? OpportunityId { get; set; }
        public int? AssignedToUserId { get; set; }
        public DateOnly InvoiceDate { get; set; }
        public DateOnly DueDate { get; set; }
        [MaxLength(180)] public string? Subject { get; set; }
        [MaxLength(80)] public string? CustomerPurchaseOrderNumber { get; set; }
        [MaxLength(80)] public string? CustomerReference { get; set; }
        [MaxLength(1000)] public string? BillingAddress { get; set; }
        [MaxLength(80)] public string? TaxRegistrationNumber { get; set; }
        [MaxLength(500)] public string? PaymentTerms { get; set; }
        [MaxLength(2000)] public string? TermsAndConditions { get; set; }
        [MaxLength(1500)] public string? CustomerNotes { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal AdjustmentAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        [Required, MaxLength(20)] public string Currency { get; set; } = "PKR";
        [MaxLength(8)] public string CurrencySymbol { get; set; } = "Rs";
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
        public int RevisionNumber { get; set; } = 1;
        public bool IsLocked { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int? RejectedByUserId { get; set; }
        [MaxLength(1500)] public string? RejectionReason { get; set; }
        public DateTime? IssuedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        [MaxLength(1500)] public string? CancellationReason { get; set; }
        public DateTime? VoidedAt { get; set; }
        [MaxLength(1500)] public string? VoidReason { get; set; }
        public int CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        [Timestamp] public byte[]? RowVersion { get; set; }
        public Customer Customer { get; set; } = null!;
        public SalesOrder? SalesOrder { get; set; }
        public SalesQuotation? SalesQuotation { get; set; }
        public Opportunity? Opportunity { get; set; }
        public User? AssignedToUser { get; set; }
        public User? ApprovedByUser { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
        public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
        [NotMapped] public bool IsEditable => Status is InvoiceStatus.Draft or InvoiceStatus.Rejected;
    }
}
