using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerDetailsViewModel
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string? PreferredName { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? NationalIdNumber { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public int? CalculatedAge { get; set; }
        public Gender Gender { get; set; }
        public string PrimaryPhone { get; set; } = string.Empty;
        public string? SecondaryPhone { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? StateProvince { get; set; }
        public string? PostalCode { get; set; }
        public string Country { get; set; } = string.Empty;
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? EmergencyContactRelation { get; set; }
        public CustomerType CustomerType { get; set; }
        public CustomerSource CustomerSource { get; set; }
        public string? OrganizationName { get; set; }
        public string? Occupation { get; set; }
        public string? PreferredSportCodes { get; set; }
        public PreferredContactMethod PreferredContactMethod { get; set; }
        public bool IsMember { get; set; }
        public string MembershipStatus { get; set; } = string.Empty;
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
        public string? BlacklistReason { get; set; }
        public string? InternalNotes { get; set; }
        public string? CustomerTags { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastVisitAt { get; set; }
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalSpent { get; set; }
        public bool CanManage { get; set; }
    }
}
