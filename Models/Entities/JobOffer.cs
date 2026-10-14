using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    /// <summary>
    /// A formal employment offer made to a shortlisted candidate (Screen 3 —
    /// Offer &amp; Hiring). The offer is always linked to the candidate's job
    /// application, which in turn is linked to the originating job posting.
    ///
    /// Statuses: Draft -> PendingAcceptance -> Accepted (or Rejected /
    /// Withdrawn / Expired). An accepted offer can be converted into an
    /// Employee, at which point the status becomes Hired and
    /// <see cref="EmployeeId"/> is set.
    /// </summary>
    public class JobOffer
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string ReferenceNumber { get; set; } = string.Empty;

        // The candidate's application this offer was issued against.
        public int JobApplicationId { get; set; }

        public JobApplication? JobApplication { get; set; }

        [Required, MaxLength(100)]
        public string Designation { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string Department { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        [Required, MaxLength(150)]
        public string WorkLocation { get; set; } = string.Empty;

        public decimal OfferedSalary { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = "PKR";

        public SalaryFrequency SalaryFrequency { get; set; } = SalaryFrequency.Monthly;

        // Planned key dates.
        public DateOnly OfferDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public DateOnly JoiningDate { get; set; }

        // Optional probation window, in months.
        public int? ProbationPeriodMonths { get; set; }

        [MaxLength(2000)]
        public string? Benefits { get; set; }

        [MaxLength(2000)]
        public string? Terms { get; set; }

        [MaxLength(2000)]
        public string? Remarks { get; set; }

        public JobOfferStatus Status { get; set; } = JobOfferStatus.Draft;

        public int? ReportingManagerId { get; set; }

        public Employee? ReportingManager { get; set; }

        // Workflow audit timestamps.
        public DateTime? IssuedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime? WithdrawnAt { get; set; }
        public DateTime? JoiningRecordedAt { get; set; }

        // Set when an accepted offer is converted into an employee record.
        public int? EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<JobOfferDocument> Documents { get; set; } = new List<JobOfferDocument>();
    }
}
