using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum OpportunityStage
    {
        Qualification,
        [Display(Name = "Needs Analysis")] NeedsAnalysis,
        [Display(Name = "Proposal Preparation")] ProposalPreparation,
        [Display(Name = "Proposal Sent")] ProposalSent,
        Negotiation,
        Won,
        Lost,
        [Display(Name = "On Hold")] OnHold
    }
}
