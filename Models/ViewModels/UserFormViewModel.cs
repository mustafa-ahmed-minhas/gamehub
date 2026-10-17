using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class UserFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "First name can contain alphabetic characters and spaces only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Last name can contain alphabetic characters and spaces only.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(150, ErrorMessage = "Email address cannot exceed 150 characters.")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.com$", ErrorMessage = "Email address must end with .com (e.g., name@gamehub.com).")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [RegularExpression(@"^0\d{10}$", ErrorMessage = "Phone number must be exactly 11 digits and start with 0 (e.g., 03001234567).")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "CNIC is required.")]
        [MaxLength(20, ErrorMessage = "CNIC cannot exceed 20 characters.")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC must be exactly 13 digits without hyphens (e.g., 3520212345678).")]
        public string? CnicNumber { get; set; }

        [Required(ErrorMessage = "Gender is required.")]
        public Gender? Gender { get; set; }

        [Required(ErrorMessage = "Date of birth is required.")]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        [MaxLength(80, ErrorMessage = "Department cannot exceed 80 characters.")]
        public string? Department { get; set; }

        [Required(ErrorMessage = "Designation is required.")]
        [MaxLength(100, ErrorMessage = "Designation cannot exceed 100 characters.")]
        public string? Designation { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; } = true;

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string? ConfirmPassword { get; set; }

        public string? ExistingProfileImageUrl { get; set; }

        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue && DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult("Date of birth cannot be in the future.", new[] { nameof(DateOfBirth) });
            }

            if (!IsEditMode && string.IsNullOrWhiteSpace(Password))
            {
                yield return new ValidationResult("Password is required.", new[] { nameof(Password) });
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(Password))
            {
                if (Password.Length < 8)
                {
                    yield return new ValidationResult("Password must be at least 8 characters.", new[] { nameof(Password) });
                }

                if (!Password.Any(char.IsUpper))
                {
                    yield return new ValidationResult("Password must contain an uppercase letter.", new[] { nameof(Password) });
                }

                if (!Password.Any(char.IsLower))
                {
                    yield return new ValidationResult("Password must contain a lowercase letter.", new[] { nameof(Password) });
                }

                if (!Password.Any(char.IsDigit))
                {
                    yield return new ValidationResult("Password must contain a number.", new[] { nameof(Password) });
                }

                if (!Password.Any(ch => !char.IsLetterOrDigit(ch)))
                {
                    yield return new ValidationResult("Password must contain a special character.", new[] { nameof(Password) });
                }
            }
        }
    }
}
