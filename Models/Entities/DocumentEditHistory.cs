using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// Append-only, field-level edit audit for financial documents. The manual booking
    /// integration writes one row per changed field so the Sales Order and Sales Invoice
    /// detail pages can show the previous value, the updated value, who changed it and when.
    /// Existing approval/revision audit entries are never modified.
    /// </summary>
    public class DocumentEditHistory
    {
        public int Id { get; set; }
        public DocumentType DocumentType { get; set; }
        public int DocumentId { get; set; }
        [Required, MaxLength(30)] public string DocumentNumber { get; set; } = string.Empty;
        [Required, MaxLength(80)] public string FieldName { get; set; } = string.Empty;
        [MaxLength(500)] public string? PreviousValue { get; set; }
        [MaxLength(500)] public string? UpdatedValue { get; set; }
        public int ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; }
        public User ChangedByUser { get; set; } = null!;
    }
}
