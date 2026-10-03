using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class DocumentApproval
    {
        public int Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public int DocumentId { get; set; }
        [Required, MaxLength(30)] public string DocumentNumber { get; set; } = string.Empty;
        public int RevisionNumber { get; set; }
        public ApprovalAction Action { get; set; }
        [MaxLength(40)] public string PreviousStatus { get; set; } = string.Empty;
        [MaxLength(40)] public string NewStatus { get; set; } = string.Empty;
        public int? SubmittedByUserId { get; set; }
        public int? ActionByUserId { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime ActionAt { get; set; }
        [MaxLength(1500)] public string? Comments { get; set; }
        [MaxLength(1500)] public string? RejectionReason { get; set; }
        public decimal FinancialTotal { get; set; }
        [MaxLength(20)] public string Currency { get; set; } = "PKR";
        public bool IsFinal { get; set; }
        [MaxLength(64)] public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public User? SubmittedByUser { get; set; }
        public User? ActionByUser { get; set; }
    }
}
