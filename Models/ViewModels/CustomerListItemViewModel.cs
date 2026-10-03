using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class CustomerListItemViewModel
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string PrimaryPhone { get; set; } = string.Empty;
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public CustomerType CustomerType { get; set; }
        public CustomerSource CustomerSource { get; set; }
        public bool IsMember { get; set; }
        public DateOnly? MembershipExpiryDate { get; set; }
        public int LoyaltyPoints { get; set; }
        public decimal OutstandingBalance { get; set; }
        public bool IsBlacklisted { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastVisitAt { get; set; }
        public string? ProfileImageUrl { get; set; }
    }
}
