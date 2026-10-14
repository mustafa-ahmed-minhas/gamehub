using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    /// <summary>
    /// Value sets for the Performance &amp; Warnings screen (Screen 5). They are
    /// grouped in one file because they only ever travel together across the
    /// case, meeting, warning, and improvement-plan records.
    ///
    /// Every value is persisted as a string (see ApplicationDbContext) so the
    /// stored data stays readable and the workflow can evolve safely.
    /// </summary>
    public enum PerformanceIssueType
    {
        Performance,
        Conduct,
        Attendance,
        Compliance,
        Other
    }

    /// <summary>How serious a case is judged to be at the time it is raised.</summary>
    public enum PerformanceSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Lifecycle of a performance case:
    /// Open -> UnderReview -> ImprovementPlan or Warning -> Resolved -> Closed.
    /// Resolved and Closed are terminal in normal use; a closed case can be
    /// reopened to Open when new information surfaces.
    /// </summary>
    public enum PerformanceCaseStatus
    {
        Open,

        [Display(Name = "Under Review")]
        UnderReview,

        [Display(Name = "Improvement Plan")]
        ImprovementPlan,

        Warning,

        Resolved,

        Closed
    }

    /// <summary>Attendance and outcome of a recorded performance meeting.</summary>
    public enum PerformanceMeetingStatus
    {
        Scheduled,
        Completed,
        Cancelled,

        [Display(Name = "No Show")]
        NoShow
    }

    /// <summary>State of a formal warning, from issuance through closure.</summary>
    public enum WarningStatus
    {
        Issued,
        Acknowledged,
        Contested,
        Escalated,
        Closed
    }

    /// <summary>Progress state of a measurable performance improvement plan.</summary>
    public enum ImprovementPlanStatus
    {
        Active,

        [Display(Name = "Under Review")]
        UnderReview,

        Completed,

        Overdue,

        Cancelled
    }
}
