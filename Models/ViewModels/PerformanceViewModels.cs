using System.ComponentModel.DataAnnotations;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Backing model for the Performance &amp; Warnings hub (Screen 5). Carries
    /// the six summary cards plus three tabbed listings (cases, warnings, and
    /// improvement plans), each with its own search, filters, sorting, and paging.
    /// </summary>
    public class PerformanceIndexViewModel
    {
        // Summary cards
        public int TotalCases { get; set; }
        public int OpenCases { get; set; }
        public int MeetingsScheduled { get; set; }
        public int ActiveWarnings { get; set; }
        public int ImprovementPlans { get; set; }
        public int ResolvedCases { get; set; }

        // Active tab: "cases" | "warnings" | "plans"
        public string ActiveTab { get; set; } = "cases";
        public bool CanManage { get; set; }

        // Cases tab
        public List<PerformanceCaseListItemViewModel> Cases { get; set; } = new();
        public string? CaseSearch { get; set; }
        public int? CaseEmployeeId { get; set; }
        public PerformanceCaseStatus? CaseStatus { get; set; }
        public PerformanceSeverity? CaseSeverity { get; set; }
        public string CaseSortBy { get; set; } = "newest";

        // Warnings tab
        public List<PerformanceWarningListItemViewModel> Warnings { get; set; } = new();
        public string? WarningSearch { get; set; }
        public WarningStatus? WarningStatus { get; set; }
        public string WarningSortBy { get; set; } = "newest";

        // Plans tab
        public List<ImprovementPlanListItemViewModel> Plans { get; set; } = new();
        public string? PlanSearch { get; set; }
        public ImprovementPlanStatus? PlanStatus { get; set; }
        public string PlanSortBy { get; set; } = "newest";

        // Shared paging (applies to the active tab)
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }

        // Filter option sources
        public List<SelectListItem> EmployeeOptions { get; set; } = new();
    }

    public class PerformanceCaseListItemViewModel
    {
        public int Id { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;

        public PerformanceIssueType IssueType { get; set; }
        public PerformanceSeverity Severity { get; set; }
        public PerformanceCaseStatus Status { get; set; }

        public DateOnly ReportedDate { get; set; }
        public string? AssignedHrName { get; set; }
        public int MeetingCount { get; set; }
        public int WarningCount { get; set; }
        public int PlanCount { get; set; }
    }

    public class PerformanceWarningListItemViewModel
    {
        public int Id { get; set; }
        public int PerformanceCaseId { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string CaseTitle { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateOnly IssueDate { get; set; }
        public WarningStatus Status { get; set; }
    }

    public class ImprovementPlanListItemViewModel
    {
        public int Id { get; set; }
        public int PerformanceCaseId { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly ReviewDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int ProgressPercent { get; set; }
        public ImprovementPlanStatus Status { get; set; }
    }

    /// <summary>
    /// Create / edit model for a performance case (Screen 5).
    /// </summary>
    public class PerformanceCaseFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        // System generated; displayed read-only.
        public string? CaseNumber { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select an employee.")]
        public int EmployeeId { get; set; }

        // Read-only context for the selected employee.
        public string? EmployeeName { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? EmployeeDepartment { get; set; }
        public string? EmployeeDesignation { get; set; }

        public PerformanceIssueType IssueType { get; set; } = PerformanceIssueType.Performance;

        [Required(ErrorMessage = "Case title is required.")]
        [MaxLength(150, ErrorMessage = "Case title cannot exceed 150 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [MaxLength(4000, ErrorMessage = "Description cannot exceed 4,000 characters.")]
        public string Description { get; set; } = string.Empty;

        public PerformanceSeverity Severity { get; set; } = PerformanceSeverity.Medium;

        [Required(ErrorMessage = "Reported date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? ReportedDate { get; set; }

        public int? AssignedHrUserId { get; set; }

        // Preserved on edit; always created as Open.
        public PerformanceCaseStatus Status { get; set; } = PerformanceCaseStatus.Open;

        public bool IsEditMode => Id.HasValue;

        public List<SelectListItem> EmployeeOptions { get; set; } = new();

        public List<SelectListItem> HrUserOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ReportedDate.HasValue && ReportedDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult(
                    "Reported date cannot be in the future.",
                    new[] { nameof(ReportedDate) });
            }
        }
    }

    /// <summary>Create / edit model for a performance meeting record.</summary>
    public class PerformanceMeetingFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue)]
        public int PerformanceCaseId { get; set; }

        public string? CaseNumber { get; set; }
        public string? EmployeeName { get; set; }

        [Required(ErrorMessage = "Meeting date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? MeetingDate { get; set; }

        [Required(ErrorMessage = "Meeting time is required.")]
        [DataType(DataType.Time)]
        public TimeOnly? MeetingTime { get; set; }

        [Required(ErrorMessage = "Discussion notes are required.")]
        [MaxLength(3000, ErrorMessage = "Discussion notes cannot exceed 3,000 characters.")]
        public string DiscussionNotes { get; set; } = string.Empty;

        [MaxLength(2000, ErrorMessage = "Outcome cannot exceed 2,000 characters.")]
        public string? Outcome { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? FollowUpDate { get; set; }

        public PerformanceMeetingStatus Status { get; set; } = PerformanceMeetingStatus.Scheduled;

        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var nowTime = TimeOnly.FromDateTime(now);

            if (!IsEditMode && MeetingDate.HasValue && MeetingDate.Value < today)
            {
                yield return new ValidationResult(
                    "Meeting date cannot be in the past.",
                    new[] { nameof(MeetingDate) });
            }

            if (!IsEditMode
                && MeetingDate.HasValue
                && MeetingTime.HasValue
                && MeetingDate.Value == today
                && MeetingTime.Value <= nowTime)
            {
                yield return new ValidationResult(
                    "Meeting time has already passed today. Choose a later time.",
                    new[] { nameof(MeetingTime) });
            }

            if (MeetingDate.HasValue
                && MeetingTime.HasValue
                && (Status == PerformanceMeetingStatus.Completed || Status == PerformanceMeetingStatus.NoShow)
                && (MeetingDate.Value > today
                    || (MeetingDate.Value == today && MeetingTime.Value > nowTime)))
            {
                yield return new ValidationResult(
                    "A meeting cannot be marked completed or no-show before its scheduled time.",
                    new[] { nameof(Status) });
            }

            if (FollowUpDate.HasValue && MeetingDate.HasValue && FollowUpDate.Value < MeetingDate.Value)
            {
                yield return new ValidationResult(
                    "Follow-up date cannot be before the meeting date.",
                    new[] { nameof(FollowUpDate) });
            }
        }
    }

    /// <summary>Create / edit model for a formal warning.</summary>
    public class PerformanceWarningFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue)]
        public int PerformanceCaseId { get; set; }

        public string? CaseNumber { get; set; }
        public string? EmployeeName { get; set; }

        [Required(ErrorMessage = "Warning reason is required.")]
        [MaxLength(3000, ErrorMessage = "Reason cannot exceed 3,000 characters.")]
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Issue date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? IssueDate { get; set; }

        [MaxLength(3000, ErrorMessage = "Response / remarks cannot exceed 3,000 characters.")]
        public string? ResponseRemarks { get; set; }

        public WarningStatus Status { get; set; } = WarningStatus.Issued;

        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IssueDate.HasValue && IssueDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult(
                    "Issue date cannot be in the future.",
                    new[] { nameof(IssueDate) });
            }
        }
    }

    /// <summary>Create / edit model for a measurable improvement plan.</summary>
    public class ImprovementPlanFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        [Range(1, int.MaxValue)]
        public int PerformanceCaseId { get; set; }

        public string? CaseNumber { get; set; }
        public string? EmployeeName { get; set; }

        [Required(ErrorMessage = "Objectives are required.")]
        [MaxLength(3000, ErrorMessage = "Objectives cannot exceed 3,000 characters.")]
        public string Objectives { get; set; } = string.Empty;

        [Required(ErrorMessage = "Measurable expectations are required.")]
        [MaxLength(3000, ErrorMessage = "Measurable expectations cannot exceed 3,000 characters.")]
        public string MeasurableExpectations { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? StartDate { get; set; }

        [Required(ErrorMessage = "Review date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? ReviewDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? EndDate { get; set; }

        [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100.")]
        public int ProgressPercent { get; set; }

        [MaxLength(3000, ErrorMessage = "Review notes cannot exceed 3,000 characters.")]
        public string? ReviewNotes { get; set; }

        [MaxLength(2000, ErrorMessage = "Outcome cannot exceed 2,000 characters.")]
        public string? Outcome { get; set; }

        public ImprovementPlanStatus Status { get; set; } = ImprovementPlanStatus.Active;

        public bool IsEditMode => Id.HasValue;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (!IsEditMode && StartDate.HasValue && StartDate.Value < today)
            {
                yield return new ValidationResult(
                    "Start date cannot be in the past.",
                    new[] { nameof(StartDate) });
            }

            if (Status == ImprovementPlanStatus.Active && StartDate.HasValue && StartDate.Value > today)
            {
                yield return new ValidationResult(
                    "An active improvement plan cannot start in the future.",
                    new[] { nameof(StartDate) });
            }

            if (StartDate.HasValue && ReviewDate.HasValue && ReviewDate.Value < StartDate.Value)
            {
                yield return new ValidationResult(
                    "Review date cannot be before the start date.",
                    new[] { nameof(ReviewDate) });
            }

            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            {
                yield return new ValidationResult(
                    "End date cannot be before the start date.",
                    new[] { nameof(EndDate) });
            }

            if (ReviewDate.HasValue && EndDate.HasValue && EndDate.Value < ReviewDate.Value)
            {
                yield return new ValidationResult(
                    "End date cannot be before the review date.",
                    new[] { nameof(EndDate) });
            }
        }
    }

    /// <summary>
    /// Case details page (Screen 5) with employee context, workflow actions,
    /// meeting history, warnings, improvement plans, and audit metadata.
    /// </summary>
    public class PerformanceCaseDetailsViewModel
    {
        public PerformanceCase Case { get; set; } = null!;

        public Employee Employee { get; set; } = null!;

        public string Initials { get; set; } = string.Empty;

        public string? AssignedHrName { get; set; }

        public bool CanManage { get; set; }

        public List<PerformanceCaseStatus> AllowedTransitions { get; set; } = new();

        public DateOnly Today { get; set; }
    }
}
