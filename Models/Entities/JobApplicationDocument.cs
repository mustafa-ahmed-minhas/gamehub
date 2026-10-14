using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class JobApplicationDocument
    {
        public int Id { get; set; }

        public int JobApplicationId { get; set; }

        public JobApplication? JobApplication { get; set; }

        [Required, MaxLength(200)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = "application/pdf";

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}