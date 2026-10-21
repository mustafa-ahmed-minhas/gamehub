using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Single source of truth for the Lead status workflow:
    /// New -> Contacted/Follow Up -> Qualified or Disqualified.
    /// Qualified is only reached through the Qualify Lead action and Converted
    /// only through the conversion workflow - forms may never set them.
    /// Disqualified/Lost leads can be reopened back to Contacted; Lost is a
    /// legacy value that existing records and reports still use, so it is kept
    /// for display but can never be set again through the workflow.
    /// </summary>
    public static class LeadStatusRules
    {
        private static readonly Dictionary<LeadStatus, LeadStatus[]> Transitions = new()
        {
            [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.FollowUp, LeadStatus.Disqualified },
            [LeadStatus.Contacted] = new[] { LeadStatus.FollowUp, LeadStatus.Qualified, LeadStatus.Disqualified },
            [LeadStatus.FollowUp] = new[] { LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Disqualified },
            [LeadStatus.Qualified] = new[] { LeadStatus.Contacted, LeadStatus.FollowUp, LeadStatus.Disqualified },
            [LeadStatus.Disqualified] = new[] { LeadStatus.Contacted },
            [LeadStatus.Lost] = new[] { LeadStatus.Contacted },
            [LeadStatus.Converted] = Array.Empty<LeadStatus>()
        };

        /// <summary>Statuses a lead currently at <paramref name="from"/> may move to (excluding a no-op).</summary>
        public static IReadOnlyList<LeadStatus> AllowedTargets(LeadStatus from)
            => Transitions.TryGetValue(from, out var targets) ? targets : Array.Empty<LeadStatus>();

        /// <summary>True when from -> to is a valid workflow step (staying on the same status is always allowed).</summary>
        public static bool CanTransition(LeadStatus from, LeadStatus to)
            => from == to || AllowedTargets(from).Contains(to);

        /// <summary>Reopening returns a disqualified or lost lead to the active workflow as Contacted.</summary>
        public static bool IsReopen(LeadStatus from, LeadStatus to)
            => from is LeadStatus.Disqualified or LeadStatus.Lost && to == LeadStatus.Contacted;
    }
}
