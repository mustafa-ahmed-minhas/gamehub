using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum SalesItemType
    {
        [Display(Name = "Court Booking")] CourtBooking,
        Membership,
        [Display(Name = "Corporate Package")] CorporatePackage,
        Tournament,
        Event,
        Coaching,
        [Display(Name = "Custom Service")] CustomService,
        Other
    }
}
