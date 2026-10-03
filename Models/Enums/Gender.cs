using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum Gender
    {
        Male,
        Female,
        Other,

        [Display(Name = "Prefer Not To Say")]
        PreferNotToSay
    }
}
