using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Enums
{
    public enum WorkplaceType
    {
        [Display(Name = "On-site")]
        OnSite,

        Hybrid,
        Remote
    }
}