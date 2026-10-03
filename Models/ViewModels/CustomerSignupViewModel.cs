using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerSignupViewModel
    {
        [Required, MaxLength(50)]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "First name can contain letters, spaces, hyphens and apostrophes only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Last name can contain letters, spaces, hyphens and apostrophes only.")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        [RegularExpression(@"^\+?[0-9][0-9\s-]{8,18}$", ErrorMessage = "Enter a valid phone number.")]
        public string PrimaryPhone { get; set; } = string.Empty;

        [MaxLength(20)]
        [RegularExpression(@"^\+?[0-9][0-9\s-]{8,18}$", ErrorMessage = "Enter a valid WhatsApp number.")]
        public string? WhatsAppNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [MustBeTrue(ErrorMessage = "You must accept the terms to create an account.")]
        public bool AcceptTerms { get; set; }

        public bool ReceiveBookingReminders { get; set; } = true;
        public bool ReceiveMarketingMessages { get; set; }
    }
}
