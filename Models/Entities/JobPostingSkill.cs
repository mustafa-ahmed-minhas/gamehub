using System.ComponentModel.DataAnnotations;

namespace GameHub.Models.Entities
{
    public class JobPostingSkill
    {
        public int Id { get; set; }

        public int JobPostingId { get; set; }

        public JobPosting? JobPosting { get; set; }

        [Required, MaxLength(80)]
        public string Skill { get; set; } = string.Empty;
    }
}