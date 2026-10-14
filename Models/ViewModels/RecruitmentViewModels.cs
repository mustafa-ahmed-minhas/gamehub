using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Backing model for the Recruitment Management hub (Screen 2). Carries the
    /// six summary cards plus the two tabbed listings (job openings and
    /// applications), each with its own filters, sorting, and paging.
    /// </summary>
    public class RecruitmentIndexViewModel
    {
        // Summary cards
        public int TotalJobOpenings { get; set; }
        public int OpenPositions { get; set; }
        public int TotalApplications { get; set; }
        public int UnderReview { get; set; }
        public int ShortlistedCandidates { get; set; }
        public int InterviewsScheduled { get; set; }

        // Active tab: "openings" | "applications"
        public string ActiveTab { get; set; } = "openings";
        public bool CanManage { get; set; }

        // Job openings tab
        public List<RecruitmentOpeningItemViewModel> Openings { get; set; } = new();
        public string? OpeningSearch { get; set; }
        public string? OpeningDepartment { get; set; }
        public EmploymentType? OpeningEmploymentType { get; set; }
        public JobPostingStatus? OpeningStatus { get; set; }
        public string OpeningSortBy { get; set; } = "newest";

        // Applications tab
        public List<RecruitmentApplicationItemViewModel> Applications { get; set; } = new();
        public string? ApplicationSearch { get; set; }
        public int? ApplicationJobPostingId { get; set; }
        public JobApplicationStatus? ApplicationStatus { get; set; }
        public string ApplicationSortBy { get; set; } = "newest";

        // Shared paging (applies to the active tab)
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }

        // Filter option sources
        public List<string> Departments { get; set; } = new();
        public List<SelectListItem> JobPostings { get; set; } = new();
    }

    public class RecruitmentOpeningItemViewModel
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public EmploymentType EmploymentType { get; set; }
        public WorkplaceType WorkplaceType { get; set; }
        public int Vacancies { get; set; }
        public int ApplicationCount { get; set; }
        public DateOnly ApplicationDeadline { get; set; }
        public JobPostingStatus Status { get; set; }
    }

    public class RecruitmentApplicationItemViewModel
    {
        public int Id { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string CandidateName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int JobPostingId { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string JobReference { get; set; } = string.Empty;
        public DateOnly ApplicationDate { get; set; }
        public JobApplicationStatus Status { get; set; }
        public int InterviewsScheduled { get; set; }
    }

    /// <summary>
    /// Job posting details page (Recruitment Management) with the linked
    /// applications and their pipeline status.
    /// </summary>
    public class JobPostingDetailsViewModel
    {
        public JobPosting Posting { get; set; } = null!;

        public List<JobApplication> Applications { get; set; } = new();

        public bool CanManage { get; set; }
    }
}
