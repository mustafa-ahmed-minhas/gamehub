using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    public class CrmIndexViewModel
    {
        public string ActiveTab { get; set; } = "overview";
        public string? Search { get; set; }
        public string ViewMode { get; set; } = "list";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; } = 1;
        public bool CanManageLeads { get; set; }
        public bool CanManageOpportunities { get; set; }
        public bool IsReadOnly { get; set; }
        public string CurrencySymbol { get; set; } = "Rs";

        public int TotalOpenLeads { get; set; }
        public int NewLeads { get; set; }
        public int QualifiedLeads { get; set; }
        public int OpportunityCount { get; set; }
        public decimal PipelineValue { get; set; }
        public decimal WeightedPipeline { get; set; }
        public int OverdueFollowUps { get; set; }
        public decimal ConversionRate { get; set; }

        public List<LeadListItemViewModel> Leads { get; set; } = new();
        public List<OpportunityListItemViewModel> Opportunities { get; set; } = new();
        public List<CrmFollowUpItemViewModel> FollowUps { get; set; } = new();
        public List<LeadActivity> RecentLeadActivities { get; set; } = new();
        public List<OpportunityActivity> RecentOpportunityActivities { get; set; } = new();
        public Dictionary<LeadStatus, int> LeadFunnel { get; set; } = new();
        public Dictionary<OpportunityStage, decimal> PipelineByStage { get; set; } = new();
        public Dictionary<LeadSource, int> LeadsBySource { get; set; } = new();
        public List<CrmOwnerPerformanceViewModel> OwnerPerformance { get; set; } = new();

        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class LeadListItemViewModel
    {
        public int Id { get; set; }
        public string LeadNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? OrganizationName { get; set; }
        public LeadSource Source { get; set; }
        public LeadServiceInterest ServiceInterest { get; set; }
        public string? SportName { get; set; }
        public string? AssignedTo { get; set; }
        public LeadStatus Status { get; set; }
        public LeadPriority Priority { get; set; }
        public LeadTemperature Temperature { get; set; }
        public DateTime? NextFollowUpAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsWebsiteInquiry { get; set; }
        public bool IsConverted { get; set; }
        public int DaysOpen { get; set; }
    }

    public class OpportunityListItemViewModel
    {
        public int Id { get; set; }
        public string OpportunityNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LeadNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public OpportunityType Type { get; set; }
        public OpportunityStage Stage { get; set; }
        public decimal ExpectedValue { get; set; }
        public int ProbabilityPercentage { get; set; }
        public decimal WeightedValue { get; set; }
        public DateOnly ExpectedCloseDate { get; set; }
        public string? AssignedTo { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class LeadFormViewModel
    {
        public int? Id { get; set; }
        public string? LeadNumber { get; set; }
        public int? CustomerId { get; set; }
        [Required, MaxLength(120)] public string CustomerName { get; set; } = string.Empty;
        [MaxLength(150), EmailAddress] public string? Email { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(20)] public string? WhatsAppNumber { get; set; }
        [MaxLength(150)] public string? OrganizationName { get; set; }
        [Required] public LeadSource Source { get; set; } = LeadSource.WalkIn;
        [Required] public LeadServiceInterest ServiceInterest { get; set; } = LeadServiceInterest.CourtBooking;
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? MembershipPlanId { get; set; }
        public DateOnly? PreferredDate { get; set; }
        public TimeOnly? PreferredStartTime { get; set; }
        [Range(1, 1440)] public int? PreferredDurationMinutes { get; set; }
        [Range(1, 1000)] public int? ExpectedPlayerCount { get; set; }
        [MaxLength(150)] public string? InquirySubject { get; set; }
        [MaxLength(2000)] public string? InquiryDetails { get; set; }
        public int? AssignedToUserId { get; set; }
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public LeadPriority Priority { get; set; } = LeadPriority.Medium;
        public LeadTemperature Temperature { get; set; } = LeadTemperature.Warm;
        public DateTime? NextFollowUpAt { get; set; }
        [MaxLength(1500)] public string? QualificationNotes { get; set; }
        [MaxLength(500)] public string? DisqualificationReason { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        [MaxLength(500)] public string? Tags { get; set; }
        public bool IsActive { get; set; } = true;
        public bool AllowDuplicate { get; set; }
        public List<LeadListItemViewModel> PossibleDuplicates { get; set; } = new();
        public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> FacilityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> MembershipPlanOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class OpportunityFormViewModel
    {
        public int? Id { get; set; }
        public string? OpportunityNumber { get; set; }
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [Required] public int LeadId { get; set; }
        public int? CustomerId { get; set; }
        [MaxLength(1000)] public string? Description { get; set; }
        public OpportunityType Type { get; set; } = OpportunityType.CourtBooking;
        public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;
        [Range(0, double.MaxValue)] public decimal ExpectedValue { get; set; }
        [Range(0, 100)] public int ProbabilityPercentage { get; set; } = 10;
        [Required] public DateOnly ExpectedCloseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        public DateOnly? ActualCloseDate { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? MembershipPlanId { get; set; }
        public int? AssignedToUserId { get; set; }
        [MaxLength(1500)] public string? CustomerRequirement { get; set; }
        [MaxLength(1500)] public string? ProposedSolution { get; set; }
        [MaxLength(1000)] public string? CompetitorInformation { get; set; }
        [MaxLength(500)] public string? LossReason { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        [MaxLength(500)] public string? Tags { get; set; }
        public bool IsActive { get; set; } = true;
        public string CurrencySymbol { get; set; } = "Rs";
        public IReadOnlyList<SelectListItem> LeadOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CustomerOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> FacilityOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> CourtOptions { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyList<SelectListItem> MembershipPlanOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class PublicInquiryViewModel
    {
        [Required, MaxLength(120)] public string FullName { get; set; } = string.Empty;
        [MaxLength(150), EmailAddress] public string? Email { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(20)] public string? WhatsAppNumber { get; set; }
        [Required] public LeadServiceInterest ServiceInterest { get; set; } = LeadServiceInterest.CourtBooking;
        public int? SportId { get; set; }
        public DateOnly? PreferredDate { get; set; }
        public TimeOnly? PreferredTime { get; set; }
        [Range(1, 1000)] public int? PlayerCount { get; set; }
        [Required, MaxLength(150)] public string InquirySubject { get; set; } = string.Empty;
        [MaxLength(2000)] public string? InquiryDetails { get; set; }
        [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept the privacy policy.")]
        public bool AcceptPrivacyPolicy { get; set; }
        public IReadOnlyList<SelectListItem> SportOptions { get; set; } = Array.Empty<SelectListItem>();
    }

    public class CrmFollowUpItemViewModel
    {
        public string RecordType { get; set; } = string.Empty;
        public int ActivityId { get; set; }
        public int ParentId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Owner { get; set; }
        public DateTime DueAt { get; set; }
        public LeadPriority Priority { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class CrmOwnerPerformanceViewModel
    {
        public string Owner { get; set; } = "Unassigned";
        public int OpenLeads { get; set; }
        public int Qualified { get; set; }
        public int Converted { get; set; }
        public decimal PipelineValue { get; set; }
    }
}
