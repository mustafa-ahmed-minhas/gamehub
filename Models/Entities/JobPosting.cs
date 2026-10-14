using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class JobPosting
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string ReferenceNumber { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string JobTitle { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string Department { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        public WorkplaceType WorkplaceType { get; set; }

        [Required, MaxLength(150)]
        public string JobLocation { get; set; } = string.Empty;

        public int Vacancies { get; set; }

        public decimal? MinimumSalary { get; set; }

        public decimal? MaximumSalary { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "PKR";

        [Required, MaxLength(80)]
        public string ExperienceRequired { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string MinimumEducation { get; set; } = string.Empty;

        [Required, MaxLength(5000)]
        public string JobDescription { get; set; } = string.Empty;

        [Required, MaxLength(3000)]
        public string KeyResponsibilities { get; set; } = string.Empty;

        [Required, MaxLength(3000)]
        public string Qualifications { get; set; } = string.Empty;

        public DateOnly ApplicationDeadline { get; set; }

        public JobPostingStatus Status { get; set; }

        public int? HiringManagerUserId { get; set; }

        public User? HiringManager { get; set; }

        [MaxLength(2000)]
        public string? Benefits { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<JobPostingSkill> Skills { get; set; } = new List<JobPostingSkill>();

        public ICollection<JobPostingScreeningQuestion> ScreeningQuestions { get; set; } = new List<JobPostingScreeningQuestion>();

        public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    }
}