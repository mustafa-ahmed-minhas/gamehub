using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum CustomerSource
    {
        WalkIn,
        Website,

        [Display(Name = "Social Media")]
        SocialMedia,

        Referral,

        [Display(Name = "Phone Call")]
        PhoneCall,

        Corporate,
        Other
    }
}
