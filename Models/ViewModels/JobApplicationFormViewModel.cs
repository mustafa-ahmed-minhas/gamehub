using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class JobApplicationFormViewModel : IValidatableObject
    {
        public const int MaxSupportingDocumentCount = 3;
        public const long MaxPdfBytes = 5 * 1024 * 1024;

        public int? Id { get; set; }

        // System generated on save; displayed read-only in the form.
        public string? ApplicationNumber { get; set; }

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
        [MaxLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [MaxLength(30, ErrorMessage = "Phone Number cannot exceed 30 characters.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "National ID Number is required.")]
        [MaxLength(30, ErrorMessage = "National ID Number cannot exceed 30 characters.")]
        public string NationalIdNumber { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateOnly? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [Required(ErrorMessage = "City is required.")]
        [MaxLength(80, ErrorMessage = "City cannot exceed 80 characters.")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Country is required.")]
        [MaxLength(80)]
        public string Country { get; set; } = "Pakistan";

        [MaxLength(250, ErrorMessage = "Current Address cannot exceed 250 characters.")]
        public string? CurrentAddress { get; set; }

        [Required(ErrorMessage = "Select a job posting.")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a valid job posting.")]
        public int JobPostingId { get; set; }

        [DataType(DataType.Date)]
        public DateOnly ApplicationDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Submitted;

        [Required(ErrorMessage = "Experience in Years is required.")]
        [Range(0, 50, ErrorMessage = "Experience must be between 0 and 50 years.")]
        public int? ExperienceYears { get; set; }

        [Required(ErrorMessage = "Highest Education is required.")]
        [MaxLength(120)]
        public string HighestEducation { get; set; } = string.Empty;

        [MaxLength(150, ErrorMessage = "Current/Previous Employer cannot exceed 150 characters.")]
        public string? CurrentEmployer { get; set; }

        [MaxLength(100, ErrorMessage = "Current Job Title cannot exceed 100 characters.")]
        public string? CurrentJobTitle { get; set; }

        [Range(0, 999999999, ErrorMessage = "Expected Salary cannot be negative.")]
        public decimal? ExpectedSalary { get; set; }

        [MaxLength(40)]
        public string? NoticePeriod { get; set; }

        [MaxLength(3000, ErrorMessage = "Cover Letter cannot exceed 3,000 characters.")]
        public string? CoverLetter { get; set; }

        [Url(ErrorMessage = "Enter a valid LinkedIn profile URL (including https://).")]
        [MaxLength(300, ErrorMessage = "LinkedIn URL cannot exceed 300 characters.")]
        public string? LinkedInUrl { get; set; }

        [Url(ErrorMessage = "Enter a valid portfolio URL (including https://).")]
        [MaxLength(300, ErrorMessage = "Portfolio URL cannot exceed 300 characters.")]
        public string? PortfolioUrl { get; set; }

        [Required(ErrorMessage = "CV / Resume (PDF) is required.")]
        [DataType(DataType.Upload)]
        public IFormFile? CvFile { get; set; }

        [DataType(DataType.Upload)]
        public List<IFormFile> SupportingDocuments { get; set; } = new();

        // Populated when editing so the existing CV / documents stay visible and
        // the CV upload can remain optional.
        public string? ExistingCvFileName { get; set; }

        public long ExistingCvFileSize { get; set; }

        public List<JobApplicationDocument> ExistingDocuments { get; set; } = new();

        public bool IsEditMode => Id.HasValue;

        public List<SelectListItem> AvailableJobPostings { get; set; } = new();

        public List<SelectListItem> Countries { get; set; } = new();

        public List<SelectListItem> EducationLevels { get; set; } = new();

        public List<SelectListItem> NoticePeriods { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (DateOfBirth.Value > today)
                {
                    yield return new ValidationResult(
                        "Date of Birth cannot be in the future.",
                        new[] { nameof(DateOfBirth) });
                }
                else
                {
                    var age = today.Year - DateOfBirth.Value.Year;
                    if (DateOfBirth.Value.AddYears(age) > today)
                    {
                        age--;
                    }

                    if (age < 18)
                    {
                        yield return new ValidationResult(
                            "Applicant must be at least 18 years old.",
                            new[] { nameof(DateOfBirth) });
                    }
                }
            }

            if (ApplicationDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult(
                    "Application Date cannot be in the future.",
                    new[] { nameof(ApplicationDate) });
            }

            // International phone: optional leading +, digits only afterwards, 7-15 digits.
            var phoneRaw = (Phone ?? string.Empty).Trim();
            var phoneDigits = new string(phoneRaw.Where(char.IsDigit).ToArray());
            var hasPlus = phoneRaw.StartsWith('+');
            var invalidChars = phoneRaw
                .Skip(hasPlus ? 1 : 0)
                .Any(c => !char.IsDigit(c) && c != '-' && c != ' ' && c != '(' && c != ')');

            if (phoneRaw.Length == 0
                || hasPlus && phoneDigits.Length == phoneRaw.Length
                || phoneDigits.Length is < 7 or > 15
                || invalidChars
                || phoneRaw.Count(c => c == '+') > 1)
            {
                yield return new ValidationResult(
                    "Enter a valid phone number with country code, e.g., +92 300 1234567.",
                    new[] { nameof(Phone) });
            }

            if (!NationalIdValidator.TryNormalize(NationalIdNumber, Country, out _, out var nationalIdError))
            {
                yield return new ValidationResult(nationalIdError, new[] { nameof(NationalIdNumber) });
            }

            if (SupportingDocuments != null && SupportingDocuments.Count > MaxSupportingDocumentCount)
            {
                yield return new ValidationResult(
                    $"A maximum of {MaxSupportingDocumentCount} supporting documents are allowed.",
                    new[] { nameof(SupportingDocuments) });
            }
        }
    }
}