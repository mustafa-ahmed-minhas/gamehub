using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class PublicCareersIndexViewModel
    {
        public IReadOnlyList<PublicJobOpeningListItemViewModel> OpenPositions { get; set; } = Array.Empty<PublicJobOpeningListItemViewModel>();

        public bool LoadFailed { get; set; }
    }

    public class PublicJobOpeningListItemViewModel
    {
        public int Id { get; set; }

        public string JobTitle { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string JobLocation { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        public WorkplaceType WorkplaceType { get; set; }

        public int Vacancies { get; set; }

        public DateOnly PostedOn { get; set; }

        public DateOnly ApplicationDeadline { get; set; }

        public string JobDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// Public, read-only projection of a job posting. Contains only the fields
    /// that are safe to show to anonymous visitors; internal HR data (hiring
    /// manager, screening questions, audit metadata, applications) is excluded.
    /// </summary>
    public class PublicJobPostingViewModel
    {
        public int Id { get; set; }

        public string ReferenceNumber { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        public WorkplaceType WorkplaceType { get; set; }

        public string JobLocation { get; set; } = string.Empty;

        public int Vacancies { get; set; }

        public decimal? MinimumSalary { get; set; }

        public decimal? MaximumSalary { get; set; }

        public string Currency { get; set; } = "PKR";

        public string ExperienceRequired { get; set; } = string.Empty;

        public string MinimumEducation { get; set; } = string.Empty;

        public string JobDescription { get; set; } = string.Empty;

        public string KeyResponsibilities { get; set; } = string.Empty;

        public string Qualifications { get; set; } = string.Empty;

        public string? Benefits { get; set; }

        public IReadOnlyList<string> Skills { get; set; } = Array.Empty<string>();

        public DateOnly PostedOn { get; set; }

        public DateOnly ApplicationDeadline { get; set; }

        public bool HasSalaryInformation => MinimumSalary.HasValue || MaximumSalary.HasValue;
    }

    /// <summary>Backing model for the public job details page.</summary>
    public class PublicJobDetailsViewModel
    {
        public PublicJobPostingViewModel Posting { get; set; } = new();

        public bool LoadFailed { get; set; }
    }

    /// <summary>
    /// Backing model for the public job application page. The candidate form is
    /// a reused <see cref="JobApplicationFormViewModel"/> so public and HR
    /// submissions share the same validation and stored fields.
    /// </summary>
    public class PublicJobApplicationViewModel
    {
        public PublicJobPostingViewModel JobPosting { get; set; } = new();

        public JobApplicationFormViewModel Form { get; set; } = new();

        /// <summary>True when the candidate already applied to this posting; the form is hidden.</summary>
        public bool DuplicateDetected { get; set; }

        public bool LoadFailed { get; set; }
    }

    /// <summary>Confirmation shown after a public application is stored.</summary>
    public class PublicJobApplicationSuccessViewModel
    {
        public string ApplicationNumber { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;

        public string ReferenceNumber { get; set; } = string.Empty;
    }
}
