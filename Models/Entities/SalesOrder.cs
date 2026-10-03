using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class SalesOrder
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string SalesOrderNumber { get; set; } = string.Empty;
        public int? SalesQuotationId { get; set; }
        public int? OpportunityId { get; set; }
        public int CustomerId { get; set; }
        public int? AssignedToUserId { get; set; }
        public DateOnly OrderDate { get; set; }
        public DateOnly? ServiceStartDate { get; set; }
        public DateOnly? ServiceEndDate { get; set; }
        public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
        [MaxLength(80)] public string? CustomerPurchaseOrderNumber { get; set; }
        [MaxLength(80)] public string? CustomerReference { get; set; }
        [MaxLength(180)] public string? Subject { get; set; }
        [MaxLength(2000)] public string? TermsAndConditions { get; set; }
        [MaxLength(1500)] public string? CustomerNotes { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        [Required, MaxLength(20)] public string Currency { get; set; } = "PKR";
        [MaxLength(8)] public string CurrencySymbol { get; set; } = "Rs";
        public bool InvoiceCreated { get; set; }
        public int? InvoiceId { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? FulfilledAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        [MaxLength(500)] public string? CancellationReason { get; set; }
        public int CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        [MaxLength(20)] public string SourceType { get; set; } = "Direct";
        public int RevisionNumber { get; set; } = 1;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int? RejectedByUserId { get; set; }
        [MaxLength(1500)] public string? RejectionReason { get; set; }
        [MaxLength(1000)] public string? LastRevisionReason { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
        public bool IsLocked { get; set; }
        [Timestamp] public byte[]? RowVersion { get; set; }

        public SalesQuotation? SalesQuotation { get; set; }
        public Opportunity? Opportunity { get; set; }
        public Customer Customer { get; set; } = null!;
        public User? AssignedToUser { get; set; }
        public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();

        [NotMapped] public bool IsEditable => Status is SalesOrderStatus.Draft or SalesOrderStatus.Rejected or SalesOrderStatus.RevisedDraft;
        [NotMapped] public bool IsReadyForInvoice => Status == SalesOrderStatus.ReadyForInvoice;
    }
}
