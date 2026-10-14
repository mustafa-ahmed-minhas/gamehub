using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum EmploymentStatus
    {
        Active,

        [Display(Name = "On Leave")]
        OnLeave,

        Inactive
    }
}