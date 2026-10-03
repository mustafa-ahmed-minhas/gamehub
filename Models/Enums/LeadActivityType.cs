using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum LeadActivityType
    {
        Note,
        [Display(Name = "Phone Call")] PhoneCall,
        WhatsApp,
        Email,
        Meeting,
        [Display(Name = "Follow Up")] FollowUp,
        [Display(Name = "Status Change")] StatusChange,
        Assignment,
        Qualification,
        Disqualification,
        Conversion
    }
}
