using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class EmployeeFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        // System generated on save; displayed read-only in the form.
        public string? EmployeeNumber { get; set; }

        public string? ExistingProfileImageUrl { get; set; }

        public IFormFile? ProfileImage { get; set; }

        public bool RemoveProfileImage { get; set; }

        // Set when the employee is created by fulfilling an accepted job offer
        // (Screen 3 — Offer & Hiring). Kept nullable so the normal employee
        // form is unaffected.
        public int? OfferId { get; set; }

        public string? OfferReference { get; set; }

        [Required(ErrorMessage = "First Name is required.")]
        [MinLength(2, ErrorMessage = "First Name must be at least 2 characters.")]
        [MaxLength(50, ErrorMessage = "First Name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Use letters, spaces, hyphens, and apostrophes only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required.")]
        [MinLength(2, ErrorMessage = "Last Name must be at least 2 characters.")]
        [MaxLength(50, ErrorMessage = "Last Name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Use letters, spaces, hyphens, and apostrophes only.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(254, ErrorMessage = "Email Address cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [MaxLength(30, ErrorMessage = "Phone Number cannot exceed 30 characters.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID / CNIC is required.")]
        [MaxLength(30, ErrorMessage = "National ID / CNIC cannot exceed 30 characters.")]
        public string NationalIdNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of Birth is required.")]
        [DataType(DataType.Date)]
        public DateOnly? DateOfBirth { get; set; }

        public Gender Gender { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        [MaxLength(80)]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Designation is required.")]
        [MaxLength(100)]
        public string Designation { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

        public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;

        [Required(ErrorMessage = "Joining Date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? JoiningDate { get; set; }

        public int? ReportingManagerId { get; set; }

        [MaxLength(250, ErrorMessage = "Employment Address cannot exceed 250 characters.")]
        public string? EmploymentAddress { get; set; }

        [MaxLength(100, ErrorMessage = "Emergency Contact Name cannot exceed 100 characters.")]
        public string? EmergencyContactName { get; set; }

        [MaxLength(30, ErrorMessage = "Emergency Contact Number cannot exceed 30 characters.")]
        public string? EmergencyContactNumber { get; set; }

        [Range(0, 999999999, ErrorMessage = "Basic Salary cannot be negative.")]
        public decimal? BasicSalary { get; set; }

        [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2,000 characters.")]
        public string? Notes { get; set; }

        public bool IsEditMode => Id.HasValue;

        /// <summary>
        /// True when this employee is being created from an accepted offer.
        /// Offer-driven hires may carry a future joining date.
        /// </summary>
        public bool IsHireFromOffer => OfferId.HasValue && !IsEditMode;

        public List<SelectListItem> Departments { get; set; } = new();

        public List<SelectListItem> Designations { get; set; } = new();

        public List<SelectListItem> ReportingManagers { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (DateOfBirth.HasValue && DateOfBirth.Value > today)
            {
                yield return new ValidationResult(
                    "Date of Birth cannot be in the future.",
                    new[] { nameof(DateOfBirth) });
            }

            if (DateOfBirth.HasValue)
            {
                var age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value > today.AddYears(-age)) age--;
                if (age < 18)
                {
                    yield return new ValidationResult(
                        "Employee must be at least 18 years old.",
                        new[] { nameof(DateOfBirth) });
                }
            }

            if (JoiningDate.HasValue && JoiningDate.Value > today && !IsHireFromOffer)
            {
                yield return new ValidationResult(
                    "Joining Date cannot be in the future.",
                    new[] { nameof(JoiningDate) });
            }

            if (!IsValidPhone(PhoneNumber))
            {
                yield return new ValidationResult(
                    "Enter a valid phone number with country code, e.g., +92 300 1234567.",
                    new[] { nameof(PhoneNumber) });
            }

            if (!string.IsNullOrWhiteSpace(EmergencyContactNumber) && !IsValidPhone(EmergencyContactNumber))
            {
                yield return new ValidationResult(
                    "Enter a valid emergency contact number with country code, e.g., +92 300 1234567.",
                    new[] { nameof(EmergencyContactNumber) });
            }

            if (!NationalIdValidator.TryNormalize(NationalIdNumber, "Pakistan", out _, out var nationalIdError))
            {
                yield return new ValidationResult(nationalIdError, new[] { nameof(NationalIdNumber) });
            }
        }

        private static bool IsValidPhone(string? value)
        {
            var phoneRaw = (value ?? string.Empty).Trim();
            var phoneDigits = new string(phoneRaw.Where(char.IsDigit).ToArray());
            var hasPlus = phoneRaw.StartsWith('+');
            var invalidChars = phoneRaw
                .Skip(hasPlus ? 1 : 0)
                .Any(c => !char.IsDigit(c) && c != '-' && c != ' ' && c != '(' && c != ')');

            return phoneRaw.Length > 0
                && phoneDigits.Length is >= 7 and <= 15
                && !invalidChars
                && phoneRaw.Count(c => c == '+') <= 1;
        }
    }
}