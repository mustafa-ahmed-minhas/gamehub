using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.ViewModels
{
    public class CustomerFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }
        public string? CustomerCode { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Use letters, spaces, hyphens, and apostrophes only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Use letters, spaces, hyphens, and apostrophes only.")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PreferredName { get; set; }

        public IFormFile? ProfileImage { get; set; }
        public string? ExistingProfileImageUrl { get; set; }
        public bool RemoveProfileImage { get; set; }

        [MaxLength(30)]
        public string? NationalIdNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [Required]
        public Gender Gender { get; set; } = Gender.PreferNotToSay;

        [Required(ErrorMessage = "Primary phone is required.")]
        [MaxLength(20)]
        public string PrimaryPhone { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? SecondaryPhone { get; set; }

        [MaxLength(20)]
        public string? WhatsAppNumber { get; set; }

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? AddressLine1 { get; set; }

        [MaxLength(200)]
        public string? AddressLine2 { get; set; }

        [MaxLength(80)]
        public string? City { get; set; }

        [MaxLength(80)]
        public string? StateProvince { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        [MaxLength(80)]
        public string Country { get; set; } = "Pakistan";

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactPhone { get; set; }

        [MaxLength(50)]
        public string? EmergencyContactRelation { get; set; }

        [Required]
        public CustomerType CustomerType { get; set; } = CustomerType.Individual;

        [Required]
        public CustomerSource CustomerSource { get; set; } = CustomerSource.WalkIn;

        [MaxLength(150)]
        public string? OrganizationName { get; set; }

        [MaxLength(100)]
        public string? Occupation { get; set; }

        public List<string> SelectedSportCodes { get; set; } = new();
        public List<SelectListItem> AvailableSports { get; set; } = new();

        [Required]
        public PreferredContactMethod PreferredContactMethod { get; set; } = PreferredContactMethod.Phone;

        public bool IsMember { get; set; }

        [MaxLength(50)]
        public string? MembershipNumber { get; set; }

        public DateOnly? MembershipStartDate { get; set; }
        public DateOnly? MembershipExpiryDate { get; set; }

        [Range(0, 999999999, ErrorMessage = "Credit limit must be zero or greater.")]
        public decimal? CreditLimit { get; set; }

        public decimal OutstandingBalance { get; set; }
        public bool AllowCredit { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Loyalty points must be zero or greater.")]
        public int LoyaltyPoints { get; set; }

        public bool ReceiveMarketingMessages { get; set; }
        public bool ReceiveBookingReminders { get; set; } = true;
        public bool IsBlacklisted { get; set; }

        [MaxLength(500)]
        public string? BlacklistReason { get; set; }

        [MaxLength(2000)]
        public string? InternalNotes { get; set; }

        [MaxLength(500)]
        public string? CustomerTags { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue && DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult("Date of birth cannot be in the future.", new[] { nameof(DateOfBirth) });
            }

            if (!HasReasonablePhoneDigits(PrimaryPhone))
            {
                yield return new ValidationResult("Enter a valid primary phone number.", new[] { nameof(PrimaryPhone) });
            }

            if (!string.IsNullOrWhiteSpace(SecondaryPhone) && !HasReasonablePhoneDigits(SecondaryPhone))
            {
                yield return new ValidationResult("Enter a valid secondary phone number.", new[] { nameof(SecondaryPhone) });
            }

            if (!string.IsNullOrWhiteSpace(WhatsAppNumber) && !HasReasonablePhoneDigits(WhatsAppNumber))
            {
                yield return new ValidationResult("Enter a valid WhatsApp number.", new[] { nameof(WhatsAppNumber) });
            }

            if (!string.IsNullOrWhiteSpace(SecondaryPhone) && NormalizePhone(SecondaryPhone) == NormalizePhone(PrimaryPhone))
            {
                yield return new ValidationResult("Secondary phone must be different from primary phone.", new[] { nameof(SecondaryPhone) });
            }

            var hasEmergencyData = !string.IsNullOrWhiteSpace(EmergencyContactName)
                || !string.IsNullOrWhiteSpace(EmergencyContactPhone)
                || !string.IsNullOrWhiteSpace(EmergencyContactRelation);
            if (hasEmergencyData)
            {
                if (string.IsNullOrWhiteSpace(EmergencyContactName))
                    yield return new ValidationResult("Emergency contact name is required.", new[] { nameof(EmergencyContactName) });
                if (string.IsNullOrWhiteSpace(EmergencyContactPhone))
                    yield return new ValidationResult("Emergency contact phone is required.", new[] { nameof(EmergencyContactPhone) });
                else if (!HasReasonablePhoneDigits(EmergencyContactPhone))
                    yield return new ValidationResult("Enter a valid emergency contact phone.", new[] { nameof(EmergencyContactPhone) });
            }

            if (CustomerType == CustomerType.Corporate && string.IsNullOrWhiteSpace(OrganizationName))
            {
                yield return new ValidationResult("Organization name is required for corporate customers.", new[] { nameof(OrganizationName) });
            }

            if (IsMember)
            {
                if (string.IsNullOrWhiteSpace(MembershipNumber))
                    yield return new ValidationResult("Membership number is required.", new[] { nameof(MembershipNumber) });
                if (!MembershipStartDate.HasValue)
                    yield return new ValidationResult("Membership start date is required.", new[] { nameof(MembershipStartDate) });
                if (!MembershipExpiryDate.HasValue)
                    yield return new ValidationResult("Membership expiry date is required.", new[] { nameof(MembershipExpiryDate) });
                if (MembershipStartDate.HasValue && MembershipExpiryDate.HasValue && MembershipExpiryDate.Value < MembershipStartDate.Value)
                    yield return new ValidationResult("Membership expiry must be after or equal to the start date.", new[] { nameof(MembershipExpiryDate) });
            }

            if (!AllowCredit && CreditLimit.HasValue && CreditLimit.Value > 0)
            {
                yield return new ValidationResult("Credit limit must be zero when credit is not allowed.", new[] { nameof(CreditLimit) });
            }

            if (IsBlacklisted)
            {
                if (string.IsNullOrWhiteSpace(BlacklistReason) || BlacklistReason.Trim().Length < 10)
                    yield return new ValidationResult("Blacklist reason must be at least 10 characters.", new[] { nameof(BlacklistReason) });
            }
        }

        private static bool HasReasonablePhoneDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length is >= 7 and <= 15;
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            var prefix = trimmed.StartsWith("+", StringComparison.Ordinal) ? "+" : string.Empty;
            return prefix + new string(trimmed.Where(char.IsDigit).ToArray());
        }
    }
}
