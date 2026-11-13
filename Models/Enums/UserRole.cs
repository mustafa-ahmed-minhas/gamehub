using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum UserRole
    {
        [Display(Name = "Admin")]
        Admin,

        [Display(Name = "Manager")]
        Manager,

        [Display(Name = "Receptionist")]
        Receptionist,

        [Display(Name = "Finance")]
        Finance,

        [Display(Name = "Sales")]
        Sales,

        [Display(Name = "CRM")]
        CRM,

        [Display(Name = "HR")]
        HR,

        [Display(Name = "Operations")]
        Operations
    }
}
