using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class BookingFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        public string? BookingNumber { get; set; }
        [Required] public int CustomerId { get; set; }
        [Required] public int SportId { get; set; }
        [Required] public int FacilityId { get; set; }
        [Required] public int CourtId { get; set; }
        public int? CustomerMembershipId { get; set; }
        public DateOnly BookingDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        [Range(1, 600)] public int DurationMinutes { get; set; }
        [Range(1, 100)] public int PlayerCount { get; set; } = 1;
        public BookingSource Source { get; set; } = BookingSource.Reception;
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public bool IsWalkIn { get; set; }
        [MaxLength(1000)] public string? CustomerNotes { get; set; }
        [MaxLength(1500)] public string? InternalNotes { get; set; }
        [MaxLength(500)] public string? SpecialRequest { get; set; }
        [MaxLength(50)] public string? ReferenceNumber { get; set; }
        [Range(0, 999999999)] public decimal ManualDiscountAmount { get; set; }
        [Range(0, 999999999)] public decimal PaidAmount { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";
        public string ArenaName { get; set; } = "GameHub Arena";
        public bool CanManualDiscount { get; set; }
        public bool IsEditMode => Id.HasValue;
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Sports { get; set; } = new();
        public List<SelectListItem> Facilities { get; set; } = new();
        public List<SelectListItem> Courts { get; set; } = new();
        public BookingPriceSummaryViewModel PriceSummary { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
                yield return new ValidationResult("End time must be after start time.", new[] { nameof(EndTime) });
            if (ManualDiscountAmount < 0)
                yield return new ValidationResult("Manual discount cannot be negative.", new[] { nameof(ManualDiscountAmount) });
        }
    }
}
