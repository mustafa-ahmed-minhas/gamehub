using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class OpportunityActivity
    {
        public int Id { get; set; }
        public int OpportunityId { get; set; }
        public OpportunityActivityType ActivityType { get; set; }
        [Required, MaxLength(150)] public string Subject { get; set; } = string.Empty;
        [MaxLength(1500)] public string? Description { get; set; }
        public DateTime ActivityDate { get; set; }
        public DateTime? FollowUpDueAt { get; set; }
        public bool IsCompleted { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Opportunity Opportunity { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
    }
}
