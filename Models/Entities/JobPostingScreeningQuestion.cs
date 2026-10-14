using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class JobPostingScreeningQuestion
    {
        public int Id { get; set; }

        public int JobPostingId { get; set; }

        public JobPosting? JobPosting { get; set; }

        [Required, MaxLength(500)]
        public string Question { get; set; } = string.Empty;
    }
}