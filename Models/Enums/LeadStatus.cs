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
        /// <summary>
        /// Legacy value kept only so existing database records and reports keep
        /// working. The workflow can never set it again; a Lost lead can only be
        /// reopened back to Contacted (see LeadStatusRules).
        /// </summary>
        Lost
    }
}
