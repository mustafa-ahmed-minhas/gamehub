using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class JobApplication
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string ApplicationNumber { get; set; } = string.Empty;

        public int JobPostingId { get; set; }

        public JobPosting? JobPosting { get; set; }

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string Phone { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string NationalIdNumber { get; set; } = string.Empty;

        public DateOnly? DateOfBirth { get; set; }

        public Gender? Gender { get; set; }

        [Required, MaxLength(80)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string Country { get; set; } = "Pakistan";

        [MaxLength(250)]
        public string? CurrentAddress { get; set; }

        public DateOnly ApplicationDate { get; set; }

        public JobApplicationStatus Status { get; set; }

        public int ExperienceYears { get; set; }

        [Required, MaxLength(120)]
        public string HighestEducation { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? CurrentEmployer { get; set; }

        [MaxLength(100)]
        public string? CurrentJobTitle { get; set; }

        public decimal? ExpectedSalary { get; set; }

        [MaxLength(40)]
        public string? NoticePeriod { get; set; }

        [MaxLength(3000)]
        public string? CoverLetter { get; set; }

        [MaxLength(300)]
        public string? LinkedInUrl { get; set; }

        [MaxLength(300)]
        public string? PortfolioUrl { get; set; }

        // CV file stored securely outside wwwroot under a generated filename.
        [Required, MaxLength(200)]
        public string CvFileName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? CvOriginalFileName { get; set; }

        [MaxLength(100)]
        public string? CvContentType { get; set; }

        public long CvFileSize { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<JobApplicationDocument> Documents { get; set; } = new List<JobApplicationDocument>();

        public ICollection<Interview> Interviews { get; set; } = new List<Interview>();

        public ICollection<JobOffer> Offers { get; set; } = new List<JobOffer>();
    }
}