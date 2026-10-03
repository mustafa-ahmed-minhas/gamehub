using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum LeadSource
    {
        [Display(Name = "Website Inquiry")] WebsiteInquiry,
        [Display(Name = "Walk In")] WalkIn,
        Phone,
        WhatsApp,
        [Display(Name = "Social Media")] SocialMedia,
        Referral,
        [Display(Name = "Existing Customer")] ExistingCustomer,
        Corporate,
        Other
    }
}
