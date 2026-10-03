using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class Lead
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string LeadNumber { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        [Required, MaxLength(120)] public string CustomerName { get; set; } = string.Empty;
        [MaxLength(150), EmailAddress] public string? Email { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(20)] public string? WhatsAppNumber { get; set; }
        [MaxLength(150)] public string? OrganizationName { get; set; }
        public LeadSource Source { get; set; }
        public LeadServiceInterest ServiceInterest { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? MembershipPlanId { get; set; }
        public DateOnly? PreferredDate { get; set; }
        public TimeOnly? PreferredStartTime { get; set; }
        public int? PreferredDurationMinutes { get; set; }
        public int? ExpectedPlayerCount { get; set; }
        [MaxLength(150)] public string? InquirySubject { get; set; }
        [MaxLength(2000)] public string? InquiryDetails { get; set; }
        public int? AssignedToUserId { get; set; }
        public LeadStatus Status { get; set; }
        public LeadPriority Priority { get; set; }
        public LeadTemperature Temperature { get; set; }
        public DateTime? NextFollowUpAt { get; set; }
        public DateTime? LastContactedAt { get; set; }
        [MaxLength(1500)] public string? QualificationNotes { get; set; }
        [MaxLength(500)] public string? DisqualificationReason { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        [MaxLength(500)] public string? Tags { get; set; }
        public bool IsConverted { get; set; }
        public int? ConvertedOpportunityId { get; set; }
        public DateTime? QualifiedAt { get; set; }
        public DateTime? DisqualifiedAt { get; set; }
        public DateTime? ConvertedAt { get; set; }
        public bool IsWebsiteInquiry { get; set; }
        public int? SubmittedByCustomerId { get; set; }
        [MaxLength(40)] public string? PublicReferenceNumber { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public Customer? Customer { get; set; }
        public Customer? SubmittedByCustomer { get; set; }
        public Sport? Sport { get; set; }
        public Facility? Facility { get; set; }
        public Court? Court { get; set; }
        public MembershipPlan? MembershipPlan { get; set; }
        public User? AssignedToUser { get; set; }
        public ICollection<LeadActivity> Activities { get; set; } = new List<LeadActivity>();
        public Opportunity? ConvertedOpportunity { get; set; }

        [NotMapped] public string ContactDisplay => !string.IsNullOrWhiteSpace(Email) ? Email! : Phone ?? WhatsAppNumber ?? "No contact";
        [NotMapped] public bool IsFollowUpOverdue => NextFollowUpAt.HasValue && NextFollowUpAt.Value < DateTime.UtcNow && Status is not LeadStatus.Converted and not LeadStatus.Disqualified and not LeadStatus.Lost;
        [NotMapped] public int DaysOpen => Math.Max(0, (DateTime.UtcNow.Date - CreatedAt.Date).Days);
    }
}
