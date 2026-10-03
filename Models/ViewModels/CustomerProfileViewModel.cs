using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerProfileViewModel
    {
        [Required, MaxLength(50)]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "First name can contain letters, spaces, hyphens and apostrophes only.")]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [RegularExpression(@"^[A-Za-z\s'-]+$", ErrorMessage = "Last name can contain letters, spaces, hyphens and apostrophes only.")]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PreferredName { get; set; }

        [Required, MaxLength(20)]
        [RegularExpression(@"^\+?[0-9][0-9\s-]{8,18}$", ErrorMessage = "Enter a valid phone number.")]
        public string PrimaryPhone { get; set; } = string.Empty;

        [MaxLength(20)]
        [RegularExpression(@"^\+?[0-9][0-9\s-]{8,18}$", ErrorMessage = "Enter a valid WhatsApp number.")]
        public string? WhatsAppNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }
        public Gender Gender { get; set; }

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

        [Required, MaxLength(80)]
        public string Country { get; set; } = "Pakistan";

        public PreferredContactMethod PreferredContactMethod { get; set; }
        public bool ReceiveBookingReminders { get; set; }
        public bool ReceiveMarketingMessages { get; set; }
        public string? ProfileImageUrl { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }
}
