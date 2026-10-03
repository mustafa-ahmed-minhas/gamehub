using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum LeadStatus
    {
        New,
        Contacted,
        [Display(Name = "Follow Up")] FollowUp,
        Qualified,
        Disqualified,
        Converted,
        Lost
    }
}
