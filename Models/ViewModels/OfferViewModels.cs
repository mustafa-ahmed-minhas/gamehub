using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Backing model for the Offer &amp; Hiring hub (Screen 3). Carries the six
    /// summary cards plus the two tabbed listings (offers and hiring records),
    /// each with its own filters, sorting, and paging.
    /// </summary>
    public class OfferIndexViewModel
    {
        // Summary cards
        public int TotalOffers { get; set; }
        public int DraftOffers { get; set; }
        public int PendingAcceptance { get; set; }
        public int AcceptedOffers { get; set; }
        public int RejectedOrWithdrawn { get; set; }
        public int PendingJoining { get; set; }

        // Active tab: "offers" | "hiring"
        public string ActiveTab { get; set; } = "offers";
        public bool CanManage { get; set; }

        // Offers tab
        public List<OfferListItemViewModel> Offers { get; set; } = new();
        public string? OfferSearch { get; set; }
        public string? OfferDepartment { get; set; }
        public JobOfferStatus? OfferStatus { get; set; }
        public string OfferSortBy { get; set; } = "newest";

        // Hiring tab
        public List<OfferListItemViewModel> Hiring { get; set; } = new();
        public string? HiringSearch { get; set; }
        public string HiringSortBy { get; set; } = "newest";

        // Shared paging (applies to the active tab)
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }

        // Filter option sources
        public List<string> Departments { get; set; } = new();
    }

    public class OfferListItemViewModel
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;

        public int JobApplicationId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string CandidateEmail { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public EmploymentType EmploymentType { get; set; }

        public decimal OfferedSalary { get; set; }
        public string Currency { get; set; } = string.Empty;
        public SalaryFrequency SalaryFrequency { get; set; }

        public DateOnly OfferDate { get; set; }
        public DateOnly ExpiryDate { get; set; }
        public DateOnly JoiningDate { get; set; }

        public JobOfferStatus Status { get; set; }

        public bool HasLetter { get; set; }
        public int DocumentCount { get; set; }

        public int? EmployeeId { get; set; }
        public string? EmployeeNumber { get; set; }
    }

    /// <summary>
    /// Create / edit model for a job offer. Candidate identity is derived from
    /// the selected application; the remaining fields are captured directly.
    /// </summary>
    public class OfferFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        // System generated; displayed read-only.
        public string? ReferenceNumber { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a candidate / application.")]
        public int JobApplicationId { get; set; }

        // Read-only context for the selected application.
        public string? ApplicationNumber { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        public string? SourceJobTitle { get; set; }
        public string? SourceJobReference { get; set; }

        [Required(ErrorMessage = "Offered Designation is required.")]
        [MaxLength(100, ErrorMessage = "Offered Designation cannot exceed 100 characters.")]
        public string Designation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Department is required.")]
        [MaxLength(80, ErrorMessage = "Department cannot exceed 80 characters.")]
        public string Department { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

        [Required(ErrorMessage = "Work Location is required.")]
        [MaxLength(150, ErrorMessage = "Work Location cannot exceed 150 characters.")]
        public string WorkLocation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Offered Salary is required.")]
        [Range(0.01, 999999999.99, ErrorMessage = "Offered Salary must be a positive amount.")]
        public decimal? OfferedSalary { get; set; }

        [Required(ErrorMessage = "Salary Currency is required.")]
        [MaxLength(10, ErrorMessage = "Currency cannot exceed 10 characters.")]
        public string Currency { get; set; } = "PKR";

        public SalaryFrequency SalaryFrequency { get; set; } = SalaryFrequency.Monthly;

        [Required(ErrorMessage = "Offer Issue Date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? OfferDate { get; set; }

        [Required(ErrorMessage = "Offer Expiry Date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? ExpiryDate { get; set; }

        [Required(ErrorMessage = "Joining Date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? JoiningDate { get; set; }

        [Range(0, 24, ErrorMessage = "Probation period must be between 0 and 24 months.")]
        public int? ProbationPeriodMonths { get; set; }

        [MaxLength(2000, ErrorMessage = "Benefits cannot exceed 2,000 characters.")]
        public string? Benefits { get; set; }

        [MaxLength(2000, ErrorMessage = "Additional Terms cannot exceed 2,000 characters.")]
        public string? Terms { get; set; }

        public int? ReportingManagerId { get; set; }

        // Preserved on edit; always created as Draft.
        public JobOfferStatus Status { get; set; } = JobOfferStatus.Draft;

        public bool IsEditMode => Id.HasValue;

        public List<SelectListItem> Applications { get; set; } = new();

        public List<SelectListItem> ReportingManagers { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (OfferDate.HasValue && ExpiryDate.HasValue && ExpiryDate.Value <= OfferDate.Value)
            {
                yield return new ValidationResult(
                    "Offer Expiry Date must be later than the offer issue date.",
                    new[] { nameof(ExpiryDate) });
            }

            if (OfferDate.HasValue && JoiningDate.HasValue && JoiningDate.Value < OfferDate.Value)
            {
                yield return new ValidationResult(
                    "Joining Date cannot be before the offer issue date.",
                    new[] { nameof(JoiningDate) });
            }
        }
    }

    /// <summary>
    /// Offer details page (Screen 3) with candidate, compensation, workflow,
    /// documents, and audit metadata.
    /// </summary>
    public class OfferDetailsViewModel
    {
        public JobOffer Offer { get; set; } = null!;

        public JobApplication Application { get; set; } = null!;

        public JobPosting? Posting { get; set; }

        public string CandidateName { get; set; } = string.Empty;

        public string Initials { get; set; } = string.Empty;

        public string? ReportingManagerName { get; set; }

        public JobOfferDocument? Letter { get; set; }

        public Employee? Employee { get; set; }

        public bool CanManage { get; set; }

        public bool CanHire { get; set; }

        public bool HasGeneratedLetter { get; set; }
    }
}
