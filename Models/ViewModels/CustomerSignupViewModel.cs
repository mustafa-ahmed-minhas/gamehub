using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerSignupViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "First name is required."), MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "First name can contain letters, spaces, hyphens and apostrophes only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required."), MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Last name can contain letters, spaces, hyphens and apostrophes only.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required."), MaxLength(150, ErrorMessage = "Email address cannot exceed 150 characters.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Primary phone number is required."), MaxLength(11, ErrorMessage = "Phone number must contain exactly 11 digits.")]
        [RegularExpression(@"^0\d{10}$", ErrorMessage = "Phone number must start with 0 and contain exactly 11 digits (e.g., 03001234567).")]
        public string PrimaryPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "WhatsApp number is required."), MaxLength(11, ErrorMessage = "WhatsApp number must contain exactly 11 digits.")]
        [RegularExpression(@"^0\d{10}$", ErrorMessage = "WhatsApp number must start with 0 and contain exactly 11 digits (e.g., 03001234567).")]
        public string WhatsAppNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "CNIC / National ID is required.")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "CNIC / National ID must be exactly 13 numeric digits without spaces or hyphens (e.g., 3520212345678).")]
        public string NationalIdNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required.")]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Please select a gender.")]
        public Gender? Gender { get; set; }

        [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [MustBeTrue(ErrorMessage = "You must accept the terms to create an account.")]
        public bool AcceptTerms { get; set; }

        public bool ReceiveBookingReminders { get; set; } = true;
        public bool ReceiveMarketingMessages { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var dateOfBirthError = DateOfBirthRules.Validate(DateOfBirth);
            if (dateOfBirthError != null)
            {
                yield return new ValidationResult(dateOfBirthError, new[] { nameof(DateOfBirth) });
            }
        }
    }
}
