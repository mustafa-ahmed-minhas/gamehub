using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Single source of truth for the Opportunity stage workflow:
    /// Qualification -> Needs Analysis -> Proposal Preparation -> Proposal Sent ->
    /// Negotiation, with Won/Lost as terminal outcomes and On Hold as a parked
    /// state that can resume into any open stage. A deal may step forward one
    /// stage, step back one stage to correct a mistake, go on hold or be lost at
    /// any open stage; Won is only reachable from Negotiation. Won and Lost are
    /// final: no further transitions are allowed.
    /// </summary>
    public static class OpportunityStageRules
    {
        public static readonly IReadOnlyList<OpportunityStage> Pipeline = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.NeedsAnalysis,
            OpportunityStage.ProposalPreparation,
            OpportunityStage.ProposalSent,
            OpportunityStage.Negotiation
        };

        private static readonly Dictionary<OpportunityStage, string> Descriptions = new()
        {
            [OpportunityStage.Qualification] = "Confirm the deal is real, funded and owned.",
            [OpportunityStage.NeedsAnalysis] = "Understand the requirement, volumes and preferred timing.",
            [OpportunityStage.ProposalPreparation] = "Build the offer, pricing and available slots.",
            [OpportunityStage.ProposalSent] = "The proposal is with the customer for review.",
            [OpportunityStage.Negotiation] = "Resolve objections and finalise commercial terms.",
            [OpportunityStage.Won] = "Deal closed successfully.",
            [OpportunityStage.Lost] = "Deal closed without a sale.",
            [OpportunityStage.OnHold] = "Paused until the customer is ready to continue."
        };

        public static bool IsOpen(OpportunityStage stage) => Pipeline.Contains(stage);

        public static bool IsTerminal(OpportunityStage stage)
            => stage is OpportunityStage.Won or OpportunityStage.Lost;

        public static int PipelineIndex(OpportunityStage stage)
        {
            for (var index = 0; index < Pipeline.Count; index++)
            {
                if (Pipeline[index] == stage) return index;
            }
            return -1;
        }

        public static OpportunityStage? NextOpenStage(OpportunityStage stage)
        {
            var index = PipelineIndex(stage);
            return index >= 0 && index < Pipeline.Count - 1 ? Pipeline[index + 1] : null;
        }

        public static OpportunityStage? PreviousOpenStage(OpportunityStage stage)
        {
            var index = PipelineIndex(stage);
            return index > 0 ? Pipeline[index - 1] : null;
        }

        /// <summary>Stages a deal at <paramref name="from"/> may move to (excluding a no-op).</summary>
        public static IReadOnlyList<OpportunityStage> AllowedTargets(OpportunityStage from)
        {
            if (IsTerminal(from)) return Array.Empty<OpportunityStage>();

            if (from == OpportunityStage.OnHold)
            {
                var resume = Pipeline.ToList();
                resume.Add(OpportunityStage.Lost);
                return resume;
            }

            var targets = new List<OpportunityStage>();
            if (NextOpenStage(from) is { } next) targets.Add(next);
            if (PreviousOpenStage(from) is { } previous) targets.Add(previous);
            if (from == OpportunityStage.Negotiation) targets.Add(OpportunityStage.Won);
            targets.Add(OpportunityStage.OnHold);
            targets.Add(OpportunityStage.Lost);
            return targets;
        }

        /// <summary>True when from -> to is a valid workflow step (staying on the same stage is always allowed).</summary>
        public static bool CanTransition(OpportunityStage from, OpportunityStage to)
            => from == to || AllowedTargets(from).Contains(to);

        public static string DescribeTargets(OpportunityStage from)
            => string.Join(", ", AllowedTargets(from).Select(x => x.GetDisplayName()));

        /// <summary>Default probability applied when a deal enters a stage.</summary>
        public static int DefaultProbability(OpportunityStage stage) => stage switch
        {
            OpportunityStage.Qualification => 10,
            OpportunityStage.NeedsAnalysis => 25,
            OpportunityStage.ProposalPreparation => 40,
            OpportunityStage.ProposalSent => 60,
            OpportunityStage.Negotiation => 80,
            OpportunityStage.Won => 100,
            OpportunityStage.Lost => 0,
            OpportunityStage.OnHold => 20,
            _ => 10
        };

        public static string Describe(OpportunityStage stage)
            => Descriptions.TryGetValue(stage, out var description) ? description : string.Empty;

        /// <summary>True when <paramref name="to"/> is a later pipeline stage than <paramref name="from"/>.</summary>
        public static bool IsForwardMove(OpportunityStage from, OpportunityStage to)
            => IsOpen(from) && IsOpen(to) && PipelineIndex(to) > PipelineIndex(from);

        /// <summary>
        /// Field prerequisites for entering a stage forward or closing a deal.
        /// Backward corrections and parking on On Hold are deliberately not
        /// gated so users can always fix a record.
        /// </summary>
        public static string? MissingEntryRequirement(OpportunityStage target, decimal expectedValue, string? customerRequirement, string? proposedSolution)
        {
            switch (target)
            {
                case OpportunityStage.ProposalPreparation:
                    if (expectedValue <= 0) return "Set the expected value before moving to Proposal Preparation.";
                    if (string.IsNullOrWhiteSpace(customerRequirement)) return "Record the customer requirement before moving to Proposal Preparation.";
                    return null;
                case OpportunityStage.ProposalSent:
                    if (string.IsNullOrWhiteSpace(proposedSolution)) return "Record the proposed solution before marking the proposal as sent.";
                    return null;
                case OpportunityStage.Negotiation:
                    if (string.IsNullOrWhiteSpace(proposedSolution)) return "Record the proposed solution before moving to Negotiation.";
                    return null;
                case OpportunityStage.Won:
                    if (expectedValue <= 0) return "Set the expected value before closing this deal as Won.";
                    if (string.IsNullOrWhiteSpace(proposedSolution)) return "Record the proposed solution before closing this deal as Won.";
                    return null;
                default:
                    return null;
            }
        }

        public static string? MissingEntryRequirement(Opportunity opportunity, OpportunityStage target)
            => MissingEntryRequirement(target, opportunity.ExpectedValue, opportunity.CustomerRequirement, opportunity.ProposedSolution);
    }
}
