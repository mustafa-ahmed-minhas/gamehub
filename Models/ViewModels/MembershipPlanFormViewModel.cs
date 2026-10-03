using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class MembershipPlanFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
        [MaxLength(500)] public string? Description { get; set; }
        public MembershipPlanType PlanType { get; set; }
        [Range(1, 120)] public int DurationMonths { get; set; } = 1;
        [Range(0, 999999999)] public decimal JoiningFee { get; set; }
        [Range(0, 999999999)] public decimal RenewalFee { get; set; }
        [Range(0, 100)] public decimal DiscountPercentage { get; set; }
        [Range(0, 10000)] public int IncludedBookingHours { get; set; }
        [Range(0, 365)] public int PriorityBookingDays { get; set; }
        public bool AllowPeakHours { get; set; }
        public bool AllowOffPeakHours { get; set; } = true;
        public bool AllSports { get; set; } = true;
        public List<string> SelectedSportCodes { get; set; } = new();
        public List<SelectListItem> AvailableSports { get; set; } = new();
        [MaxLength(1000)] public string? Benefits { get; set; }
        [Range(0, 9999)] public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string CurrencySymbol { get; set; } = "PKR";
        public string ArenaName { get; set; } = "GameHub Arena";
        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!AllowPeakHours && !AllowOffPeakHours)
                yield return new ValidationResult("At least one of peak or off-peak access must be enabled.", new[] { nameof(AllowPeakHours) });
        }
    }
}
