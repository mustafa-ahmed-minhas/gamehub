using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Maps the Termination &amp; Clearance values (request status and clearance
    /// status) to the HR badge tone classes used across the Recruitment,
    /// Employee, and Performance screens (see hr.css).
    /// </summary>
    public static class TerminationStatusExtensions
    {
        /// <summary>Termination request lifecycle tone.</summary>
        public static string GetBadgeClass(this TerminationStatus status) => status switch
        {
            TerminationStatus.Draft => "draft",
            TerminationStatus.PendingApproval => "medium",
            TerminationStatus.Approved => "offer",
            TerminationStatus.Rejected => "high",
            TerminationStatus.InProgress => "progress",
            TerminationStatus.Completed => "new",
            _ => "draft"
        };

        /// <summary>Single clearance item tone.</summary>
        public static string GetBadgeClass(this ClearanceStatus status) => status switch
        {
            ClearanceStatus.Pending => "draft",
            ClearanceStatus.InProgress => "progress",
            ClearanceStatus.Cleared => "offer",
            ClearanceStatus.Outstanding => "high",
            ClearanceStatus.Rejected => "action",
            _ => "draft"
        };
    }
}
