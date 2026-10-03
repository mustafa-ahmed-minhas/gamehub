using GameHub.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameHub.Models.Entities
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PreferredName { get; set; }

        [MaxLength(300)]
        public string? ProfileImageUrl { get; set; }

        [MaxLength(30)]
        public string? NationalIdNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public Gender Gender { get; set; }

        [Required, MaxLength(20)]
        public string PrimaryPhone { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? SecondaryPhone { get; set; }

        [MaxLength(20)]
        public string? WhatsAppNumber { get; set; }

        [MaxLength(150), EmailAddress]
        public string? Email { get; set; }

        public string? PasswordHash { get; set; }

        public bool HasOnlineAccount { get; set; }

        public bool EmailVerified { get; set; }

        [MaxLength(500)]
        public string? EmailVerificationToken { get; set; }

        public DateTime? EmailVerificationTokenExpiresAt { get; set; }

        [MaxLength(500)]
        public string? PasswordResetToken { get; set; }

        public DateTime? PasswordResetTokenExpiresAt { get; set; }

        public DateTime? LastOnlineLoginAt { get; set; }

        public DateTime? OnlineAccountCreatedAt { get; set; }

        public int FailedLoginAttempts { get; set; }

        public DateTime? LockoutEndAt { get; set; }

        public DateTime? PasswordChangedAt { get; set; }

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

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactPhone { get; set; }

        [MaxLength(50)]
        public string? EmergencyContactRelation { get; set; }

        public CustomerType CustomerType { get; set; }

        public CustomerSource CustomerSource { get; set; }

        [MaxLength(150)]
        public string? OrganizationName { get; set; }

        [MaxLength(100)]
        public string? Occupation { get; set; }

        [MaxLength(300)]
        public string? PreferredSportCodes { get; set; }

        public PreferredContactMethod PreferredContactMethod { get; set; }

        public bool IsMember { get; set; }

        [MaxLength(50)]
        public string? MembershipNumber { get; set; }

        public DateOnly? MembershipStartDate { get; set; }

        public DateOnly? MembershipExpiryDate { get; set; }

        public decimal? CreditLimit { get; set; }

        public decimal OutstandingBalance { get; set; }

        public int LoyaltyPoints { get; set; }

        public bool AllowCredit { get; set; }

        public bool ReceiveMarketingMessages { get; set; }

        public bool ReceiveBookingReminders { get; set; }

        public bool IsBlacklisted { get; set; }

        [MaxLength(500)]
        public string? BlacklistReason { get; set; }

        [MaxLength(2000)]
        public string? InternalNotes { get; set; }

        [MaxLength(500)]
        public string? CustomerTags { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastVisitAt { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [NotMapped]
        public string Initials => string.Concat(new[] { FirstName, LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Take(2)
            .Select(part => char.ToUpperInvariant(part.Trim()[0])));

        [NotMapped]
        public string MembershipStatus
        {
            get
            {
                if (!IsMember) return "Non-Member";
                if (MembershipExpiryDate.HasValue && MembershipExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow)) return "Expired";
                return "Active Member";
            }
        }
    }
}
