using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum OpportunityType
    {
        [Display(Name = "Court Booking")] CourtBooking,
        Membership,
        [Display(Name = "Corporate Package")] CorporatePackage,
        Tournament,
        Event,
        Coaching,
        Other
    }
}
