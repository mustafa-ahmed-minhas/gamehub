using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class CourtScheduleFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Court is required.")] public int CourtId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpeningTime { get; set; }
        public TimeOnly ClosingTime { get; set; }
        public bool IsClosed { get; set; }
        [Range(1, 1440)] public int SlotDurationMinutes { get; set; } = 60;
        [Range(0, 1440)] public int BufferMinutes { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
        public bool IsEditMode => Id.HasValue;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!IsClosed && OpeningTime >= ClosingTime)
            {
                yield return new ValidationResult("Opening time must be before closing time.", new[] { nameof(ClosingTime) });
            }
        }
    }
}
