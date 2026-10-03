using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class CourtPricingFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Court is required.")] public int CourtId { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        [Range(1, 1440)] public int DurationMinutes { get; set; } = 60;
        [Range(0, 999999)] public decimal Price { get; set; }
        public bool IsPeakRate { get; set; }
        public bool IsActive { get; set; } = true;
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
        public bool IsEditMode => Id.HasValue;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTime >= EndTime)
            {
                yield return new ValidationResult("Start time must be before end time.", new[] { nameof(EndTime) });
            }
        }
    }
}
