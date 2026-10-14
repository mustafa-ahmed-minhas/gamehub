using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A PDF attached to a termination request (Screen 6): either the system
    /// termination letter (<see cref="IsLetter"/> true) or a supporting document
    /// uploaded by HR. Files are stored outside <c>wwwroot</c> under a generated
    /// filename and are only reachable through authorized controller actions.
    /// </summary>
    public class TerminationDocument
    {
        public int Id { get; set; }

        public int TerminationRequestId { get; set; }

        public TerminationRequest? TerminationRequest { get; set; }

        [Required, MaxLength(200)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = "application/pdf";

        public long FileSize { get; set; }

        public bool IsLetter { get; set; }

        public bool IsGenerated { get; set; }

        [MaxLength(150)]
        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
