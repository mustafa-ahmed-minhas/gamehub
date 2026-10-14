using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum EmploymentType
    {
        [Display(Name = "Full-Time")]
        FullTime,

        [Display(Name = "Part-Time")]
        PartTime,

        Contract,
        Internship
    }
}