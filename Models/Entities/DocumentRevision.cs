using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class DocumentRevision
    {
        public int Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public int DocumentId { get; set; }
        [Required, MaxLength(30)] public string DocumentNumber { get; set; } = string.Empty;
        public int RevisionNumber { get; set; }
        [Required] public string SnapshotJson { get; set; } = string.Empty;
        [Required, MaxLength(1000)] public string RevisionReason { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public User CreatedByUser { get; set; } = null!;
    }
}
