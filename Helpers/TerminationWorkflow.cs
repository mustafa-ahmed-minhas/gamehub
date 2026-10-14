using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Central workflow rules for Screen 6 — Termination &amp; Clearance. The
    /// controller validates every status change against these maps and the views
    /// render only the permitted targets, so an invalid transition can never be
    /// driven from the UI or forged through a posted form.
    /// </summary>
    public static class TerminationWorkflow
    {
        /// <summary>
        /// Request lifecycle:
        /// Draft -&gt; PendingApproval -&gt; Approved / Rejected -&gt; InProgress
        /// -&gt; Completed. Rejected and Completed are terminal.
        /// </summary>
        private static readonly Dictionary<TerminationStatus, TerminationStatus[]> RequestTransitions = new()
        {
            [TerminationStatus.Draft] = new[] { TerminationStatus.PendingApproval },
            [TerminationStatus.PendingApproval] = new[] { TerminationStatus.Approved, TerminationStatus.Rejected },
            [TerminationStatus.Approved] = new[] { TerminationStatus.InProgress },
            [TerminationStatus.Rejected] = Array.Empty<TerminationStatus>(),
            [TerminationStatus.InProgress] = new[] { TerminationStatus.Completed },
            [TerminationStatus.Completed] = Array.Empty<TerminationStatus>()
        };

        /// <summary>
        /// Clearance item lifecycle:
        /// Pending -&gt; InProgress -&gt; Cleared, with Outstanding / Rejected
        /// available when a department cannot clear the item.
        /// </summary>
        private static readonly Dictionary<ClearanceStatus, ClearanceStatus[]> ItemTransitions = new()
        {
            [ClearanceStatus.Pending] = new[]
            {
                ClearanceStatus.InProgress,
                ClearanceStatus.Cleared,
                ClearanceStatus.Outstanding,
                ClearanceStatus.Rejected
            },
            [ClearanceStatus.InProgress] = new[]
            {
                ClearanceStatus.Cleared,
                ClearanceStatus.Outstanding,
                ClearanceStatus.Rejected
            },
            [ClearanceStatus.Cleared] = new[] { ClearanceStatus.InProgress },
            [ClearanceStatus.Outstanding] = new[]
            {
                ClearanceStatus.InProgress,
                ClearanceStatus.Cleared,
                ClearanceStatus.Rejected
            },
            [ClearanceStatus.Rejected] = new[] { ClearanceStatus.InProgress, ClearanceStatus.Pending }
        };

        public static bool CanTransition(TerminationStatus from, TerminationStatus to) =>
            RequestTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

        public static IReadOnlyList<TerminationStatus> NextStatuses(TerminationStatus from) =>
            RequestTransitions.TryGetValue(from, out var targets) ? targets : Array.Empty<TerminationStatus>();

        public static bool CanTransition(ClearanceStatus from, ClearanceStatus to) =>
            ItemTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

        public static IReadOnlyList<ClearanceStatus> NextStatuses(ClearanceStatus from) =>
            ItemTransitions.TryGetValue(from, out var targets) ? targets : Array.Empty<ClearanceStatus>();
    }
}
