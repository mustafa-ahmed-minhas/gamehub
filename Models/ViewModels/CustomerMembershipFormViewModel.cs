using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class CustomerMembershipFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        public string? MembershipNumber { get; set; }
        [Required] public int CustomerId { get; set; }
        [Required] public int MembershipPlanId { get; set; }
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly ExpiryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1);
        public MembershipStatus Status { get; set; } = MembershipStatus.Active;
        [Range(0, 999999999)] public decimal JoiningFee { get; set; }
        [Range(0, 999999999)] public decimal RenewalFee { get; set; }
        [Range(0, 100)] public decimal DiscountPercentage { get; set; }
        [Range(0, 10000)] public int IncludedBookingHours { get; set; }
        [Range(0, 10000)] public int UsedBookingHours { get; set; }
        public DateOnly? FrozenFrom { get; set; }
        public DateOnly? FrozenUntil { get; set; }
        [MaxLength(500)] public string? CancellationReason { get; set; }
        [MaxLength(1000)] public string? Notes { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsActive { get; set; } = true;
        public List<SelectListItem> Customers { get; set; } = new();
        public List<SelectListItem> Plans { get; set; } = new();
        public string CurrencySymbol { get; set; } = "PKR";
        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ExpiryDate < StartDate) yield return new ValidationResult("Expiry date must be after or equal to start date.", new[] { nameof(ExpiryDate) });
            if (UsedBookingHours > IncludedBookingHours) yield return new ValidationResult("Used hours cannot exceed included hours.", new[] { nameof(UsedBookingHours) });
            if (FrozenFrom.HasValue && FrozenUntil.HasValue && FrozenUntil.Value < FrozenFrom.Value) yield return new ValidationResult("Freeze end date must be after freeze start date.", new[] { nameof(FrozenUntil) });
            if (Status == MembershipStatus.Cancelled && string.IsNullOrWhiteSpace(CancellationReason)) yield return new ValidationResult("Cancellation reason is required when cancelling.", new[] { nameof(CancellationReason) });
        }
    }
}
