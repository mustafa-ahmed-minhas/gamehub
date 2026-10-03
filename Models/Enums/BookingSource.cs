using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum BookingSource
    {
        Reception,
        [Display(Name = "Walk In")]
        WalkIn,
        Phone,
        Website,
        WhatsApp,
        [Display(Name = "Social Media")]
        SocialMedia,
        Corporate,
        Other
    }
}
