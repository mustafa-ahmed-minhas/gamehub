using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A recorded people-management case (Screen 5 — Performance &amp; Warnings):
    /// a performance, conduct, attendance, or compliance concern raised against
    /// an existing employee.
    ///
    /// A case owns its own history — meetings, formal warnings, and improvement
    /// plans — and moves through the workflow
    /// Open -> UnderReview -> ImprovementPlan / Warning -> Resolved -> Closed.
    /// </summary>
    public class PerformanceCase
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string CaseNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        public PerformanceIssueType IssueType { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string Description { get; set; } = string.Empty;

        public PerformanceSeverity Severity { get; set; } = PerformanceSeverity.Medium;

        public DateOnly ReportedDate { get; set; }

        // HR owner responsible for driving the case to resolution.
        public int? AssignedHrUserId { get; set; }

        public User? AssignedHrUser { get; set; }

        public PerformanceCaseStatus Status { get; set; } = PerformanceCaseStatus.Open;

        [MaxLength(4000)]
        public string? ResolutionNotes { get; set; }

        // Workflow audit timestamps.
        public DateTime? ResolvedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<PerformanceMeeting> Meetings { get; set; } = new List<PerformanceMeeting>();

        public ICollection<PerformanceWarning> Warnings { get; set; } = new List<PerformanceWarning>();

        public ICollection<ImprovementPlan> ImprovementPlans { get; set; } = new List<ImprovementPlan>();
    }
}
