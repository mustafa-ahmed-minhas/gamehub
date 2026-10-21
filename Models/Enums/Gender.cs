using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum Gender
    {
        Male = 0,
        Female = 1,
        // 2 (Other) was intentionally removed. PreferNotToSay keeps its
        // original persisted value of 3 so existing database rows stay valid.
        [Display(Name = "Prefer Not To Say")]
        PreferNotToSay = 3
    }
}
