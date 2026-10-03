using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum LeadServiceInterest
    {
        [Display(Name = "Court Booking")] CourtBooking,
        Membership,
        [Display(Name = "Corporate Booking")] CorporateBooking,
        Tournament,
        Event,
        Coaching,
        [Display(Name = "General Inquiry")] GeneralInquiry,
        Other
    }
}
