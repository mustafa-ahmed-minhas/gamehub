using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class Opportunity
    {
        public int Id { get; set; }
        [Required, MaxLength(30)] public string OpportunityNumber { get; set; } = string.Empty;
        public int LeadId { get; set; }
        public int? CustomerId { get; set; }
        public int? SportId { get; set; }
        public int? FacilityId { get; set; }
        public int? CourtId { get; set; }
        public int? MembershipPlanId { get; set; }
        public int? AssignedToUserId { get; set; }
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(1000)] public string? Description { get; set; }
        public OpportunityType Type { get; set; }
        public OpportunityStage Stage { get; set; }
        public decimal ExpectedValue { get; set; }
        public int ProbabilityPercentage { get; set; }
        public DateOnly ExpectedCloseDate { get; set; }
        public DateOnly? ActualCloseDate { get; set; }
        [MaxLength(1500)] public string? CustomerRequirement { get; set; }
        [MaxLength(1500)] public string? ProposedSolution { get; set; }
        [MaxLength(1000)] public string? CompetitorInformation { get; set; }
        [MaxLength(500)] public string? LossReason { get; set; }
        [MaxLength(2000)] public string? InternalNotes { get; set; }
        [MaxLength(500)] public string? Tags { get; set; }
        public bool QuotationCreated { get; set; }
        public int? QuotationId { get; set; }
        public bool IsActive { get; set; } = true;
        public int CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? WonAt { get; set; }
        public DateTime? LostAt { get; set; }

        public Lead Lead { get; set; } = null!;
        public Customer? Customer { get; set; }
        public Sport? Sport { get; set; }
        public Facility? Facility { get; set; }
        public Court? Court { get; set; }
        public MembershipPlan? MembershipPlan { get; set; }
        public User? AssignedToUser { get; set; }
        public ICollection<OpportunityActivity> Activities { get; set; } = new List<OpportunityActivity>();

        [NotMapped] public decimal WeightedValue => Math.Round(ExpectedValue * ProbabilityPercentage / 100m, 2);
        [NotMapped] public int DaysUntilClose => ExpectedCloseDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        [NotMapped] public bool IsOverdue => Stage is not OpportunityStage.Won and not OpportunityStage.Lost && ExpectedCloseDate < DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
