using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum OpportunityActivityType
    {
        Note,
        [Display(Name = "Phone Call")] PhoneCall,
        WhatsApp,
        Email,
        Meeting,
        [Display(Name = "Follow Up")] FollowUp,
        [Display(Name = "Stage Change")] StageChange,
        Assignment,
        Proposal,
        Negotiation,
        Won,
        Lost
    }
}
