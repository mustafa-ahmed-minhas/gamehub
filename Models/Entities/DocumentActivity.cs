using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class DocumentActivity
    {
        public int Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public int DocumentId { get; set; }
        public ApprovalAction ActivityType { get; set; }
        [Required, MaxLength(1000)] public string Description { get; set; } = string.Empty;
        public int PerformedByUserId { get; set; }
        public DateTime PerformedAt { get; set; }
        public User PerformedByUser { get; set; } = null!;
    }
}
