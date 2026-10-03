using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class SalesQuotation
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string QuotationNumber { get; set; } = string.Empty;
        public int OpportunityId { get; set; }
        public int CustomerId { get; set; }
        public int? AssignedToUserId { get; set; }
        public DateOnly QuotationDate { get; set; }
        public DateOnly ValidUntil { get; set; }
        public SalesQuotationStatus Status { get; set; } = SalesQuotationStatus.Draft;
        [MaxLength(180)] public string? Subject { get; set; }
        [MaxLength(1500)] public string? Introduction { get; set; }
        [MaxLength(2000)] public string? TermsAndConditions { get; set; }
        [MaxLength(1500)] public string? CustomerNotes { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        [MaxLength(80)] public string? CustomerReference { get; set; }
        public decimal Subtotal { get; set; }
        public decimal LineDiscountTotal { get; set; }
        public decimal ManualDiscountAmount { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        [Required, MaxLength(20)] public string Currency { get; set; } = "PKR";
        [MaxLength(8)] public string CurrencySymbol { get; set; } = "Rs";
        public bool SalesOrderCreated { get; set; }
        public int? SalesOrderId { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? ViewedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        [MaxLength(500)] public string? RejectionReason { get; set; }
        [MaxLength(500)] public string? CancellationReason { get; set; }
        public int CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int RevisionNumber { get; set; } = 1;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public int? RejectedByUserId { get; set; }
        [MaxLength(1000)] public string? LastRevisionReason { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
        public bool IsLocked { get; set; }
        [Timestamp] public byte[]? RowVersion { get; set; }

        public Opportunity Opportunity { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
        public User? AssignedToUser { get; set; }
        public ICollection<SalesQuotationItem> Items { get; set; } = new List<SalesQuotationItem>();

        [NotMapped] public bool IsExpired => Status is SalesQuotationStatus.SubmittedForApproval or SalesQuotationStatus.Approved && ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow);
        [NotMapped] public int RemainingValidityDays => ValidUntil.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        [NotMapped] public bool IsEditable => Status is SalesQuotationStatus.Draft or SalesQuotationStatus.Rejected or SalesQuotationStatus.RevisedDraft;
        [NotMapped] public bool IsReadyForSalesOrder => Status == SalesQuotationStatus.AcceptedByCustomer && !SalesOrderCreated;
    }
}
