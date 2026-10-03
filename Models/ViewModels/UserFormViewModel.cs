using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class UserFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "First name can contain letters, spaces, hyphens, and apostrophes only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Last name can contain letters, spaces, hyphens, and apostrophes only.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(150, ErrorMessage = "Email address cannot exceed 150 characters.")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [RegularExpression(@"^(\+92|0092|0)?[0-9\-\s]{10,17}$", ErrorMessage = "Enter a valid Pakistani or international phone number.")]
        public string? PhoneNumber { get; set; }

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
