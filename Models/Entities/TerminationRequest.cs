using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A termination / offboarding request (Screen 6 — Termination &amp;
    /// Clearance). Always linked to an existing employee, carries the reason and
    /// proposed last working day, a securely stored termination letter, a
    /// clearance checklist, and an immutable status history.
    ///
    /// Workflow: Draft -> PendingApproval -> Approved / Rejected -> InProgress
    /// -> Completed. The employee's history is never removed; completing a
    /// request only updates their employment status.
    /// </summary>
    public class TerminationRequest
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string ReferenceNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        public TerminationType TerminationType { get; set; }

        [Required, MaxLength(200)]
        public string Reason { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string Explanation { get; set; } = string.Empty;

        public DateOnly RequestDate { get; set; }

        public DateOnly ProposedLastWorkingDay { get; set; }

        [MaxLength(50)]
        public string? NoticePeriod { get; set; }

        public TerminationStatus Status { get; set; } = TerminationStatus.Draft;

        [MaxLength(3000)]
        public string? Remarks { get; set; }

        // Approval / rejection decision captured by the authorized approver.
        [MaxLength(3000)]
        public string? DecisionRemarks { get; set; }

        public DateTime? DecisionAt { get; set; }

        public int? DecidedByUserId { get; set; }

        public User? DecidedByUser { get; set; }

        public DateTime? CompletedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<TerminationDocument> Documents { get; set; } = new List<TerminationDocument>();

        public ICollection<ClearanceItem> ClearanceItems { get; set; } = new List<ClearanceItem>();

        public ICollection<TerminationStatusHistory> StatusHistory { get; set; } = new List<TerminationStatusHistory>();
    }
}
