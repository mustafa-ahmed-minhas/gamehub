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
        [RegularExpression(HrValidationRules.PersonNamePattern, ErrorMessage = HrValidationRules.PersonNameMessage)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required.")]
        [MinLength(2, ErrorMessage = "Last Name must be at least 2 characters.")]
        [MaxLength(50, ErrorMessage = "Last Name cannot exceed 50 characters.")]
        [RegularExpression(HrValidationRules.PersonNamePattern, ErrorMessage = HrValidationRules.PersonNameMessage)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email Address is required.")]
        [EmailAddress(ErrorMessage = HrValidationRules.EmailMessage)]
        [RegularExpression(HrValidationRules.EmailPattern, ErrorMessage = HrValidationRules.EmailMessage)]
        [MaxLength(254, ErrorMessage = "Email Address cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(HrValidationRules.PhonePattern, ErrorMessage = HrValidationRules.PhoneMessage)]
        [MaxLength(11, ErrorMessage = "Phone number must contain exactly 11 digits.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID / CNIC is required.")]
        [RegularExpression(HrValidationRules.NationalIdPattern, ErrorMessage = HrValidationRules.NationalIdMessage)]
        public string NationalIdNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of Birth is required.")]
        [DataType(DataType.Date)]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Gender is required.")]
        public Gender? Gender { get; set; }

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
        [RegularExpression(HrValidationRules.PersonNamePattern, ErrorMessage = HrValidationRules.PersonNameMessage)]
        public string? EmergencyContactName { get; set; }

        [RegularExpression(HrValidationRules.PhonePattern, ErrorMessage = HrValidationRules.PhoneMessage)]
        [MaxLength(11, ErrorMessage = "Phone number must contain exactly 11 digits.")]
        public string? EmergencyContactNumber { get; set; }

        [Range(0, 999999999.99, ErrorMessage = "Basic Salary must be between 0 and 999,999,999.99.")]
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

            // Shared 16+ floor first; HR keeps its existing, stricter 18+ policy.
            var dateOfBirthError = DateOfBirthRules.Validate(
                DateOfBirth,
                minimumAge: 18,
                minimumAgeMessage: "Employee must be at least 18 years old.",
                today: today);
            if (dateOfBirthError != null)
            {
                yield return new ValidationResult(dateOfBirthError, new[] { nameof(DateOfBirth) });
            }

            if (JoiningDate.HasValue && JoiningDate.Value > today && !IsHireFromOffer)
            {
                yield return new ValidationResult(
                    "Joining Date cannot be in the future.",
                    new[] { nameof(JoiningDate) });
            }

            if (DateOfBirth.HasValue && JoiningDate.HasValue)
            {
                var eighteenthBirthday = DateOfBirth.Value.AddYears(18);
                if (JoiningDate.Value < eighteenthBirthday)
                {
                    yield return new ValidationResult(
                        "Joining Date must be on or after the employee's 18th birthday.",
                        new[] { nameof(JoiningDate) });
                }
            }

            // Phone validation handled by data annotations
            // Emergency contact phone validation handled by data annotations
            // National ID validation handled by data annotations
        }
    }
}