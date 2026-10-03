using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum UserRole
    {
        [Display(Name = "Super Admin")]
        SuperAdmin,

        [Display(Name = "Admin")]
        Admin,

        [Display(Name = "Booking Manager")]
        BookingManager,

        [Display(Name = "Court Manager")]
        CourtManager,

        [Display(Name = "Finance Manager")]
        FinanceManager,

        [Display(Name = "Staff Manager")]
        StaffManager,

        [Display(Name = "Receptionist")]
        Receptionist,

        [Display(Name = "Viewer")]
        Viewer
    }
}
