using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Maps the Performance &amp; Warnings values (case status, severity,
    /// meeting status, warning status, and improvement-plan status) to the HR
    /// badge tone classes used across the Recruitment and Employee screens
    /// (see hr.css).
    /// </summary>
    public static class PerformanceStatusExtensions
    {
        /// <summary>Case lifecycle tone.</summary>
        public static string GetBadgeClass(this PerformanceCaseStatus status) => status switch
        {
            PerformanceCaseStatus.Open => "open",
            PerformanceCaseStatus.UnderReview => "review",
            PerformanceCaseStatus.ImprovementPlan => "progress",
            PerformanceCaseStatus.Warning => "medium",
            PerformanceCaseStatus.Resolved => "offer",
            PerformanceCaseStatus.Closed => "draft",
            _ => "new"
        };

        /// <summary>Severity tone: green through to red as it escalates.</summary>
        public static string GetBadgeClass(this PerformanceSeverity severity) => severity switch
        {
            PerformanceSeverity.Low => "low",
            PerformanceSeverity.Medium => "medium",
            PerformanceSeverity.High => "high",
            PerformanceSeverity.Critical => "action",
            _ => "assigned"
        };

        public static string GetBadgeClass(this PerformanceMeetingStatus status) => status switch
        {
            PerformanceMeetingStatus.Scheduled => "interview",
            PerformanceMeetingStatus.Completed => "offer",
            PerformanceMeetingStatus.Cancelled => "high",
            PerformanceMeetingStatus.NoShow => "action",
            _ => "new"
        };

        public static string GetBadgeClass(this WarningStatus status) => status switch
        {
            WarningStatus.Issued => "action",
            WarningStatus.Acknowledged => "medium",
            WarningStatus.Contested => "review",
            WarningStatus.Escalated => "high",
            WarningStatus.Closed => "draft",
            _ => "new"
        };

        public static string GetBadgeClass(this ImprovementPlanStatus status) => status switch
        {
            ImprovementPlanStatus.Active => "progress",
            ImprovementPlanStatus.UnderReview => "review",
            ImprovementPlanStatus.Completed => "offer",
            ImprovementPlanStatus.Overdue => "high",
            ImprovementPlanStatus.Cancelled => "draft",
            _ => "new"
        };
    }
}
