using System.ComponentModel.DataAnnotations;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Backing model for the Termination &amp; Clearance hub (Screen 6). Presents
    /// the six KPI cards, the filterable / sortable / paged request list, and the
    /// filter option lists.
    /// </summary>
    public class TerminationIndexViewModel
    {
        // Summary cards.
        public int TotalRequests { get; set; }
        public int PendingApproval { get; set; }
        public int Approved { get; set; }
        public int InProgress { get; set; }
        public int PendingClearance { get; set; }
        public int Completed { get; set; }

        public bool CanManage { get; set; }

        public List<TerminationListItemViewModel> Requests { get; set; } = new();

        public string? Search { get; set; }
        public int? EmployeeId { get; set; }
        public TerminationStatus? Status { get; set; }
        public TerminationType? TerminationType { get; set; }
        public string SortBy { get; set; } = "newest";

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }

        public List<SelectListItem> EmployeeOptions { get; set; } = new();
    }

    /// <summary>One row in the termination request list.</summary>
    public class TerminationListItemViewModel
    {
        public int Id { get; set; }

        public string ReferenceNumber { get; set; } = string.Empty;

        public int EmployeeId { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public string EmployeeNumber { get; set; } = string.Empty;

        public string Initials { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public string Designation { get; set; } = string.Empty;

        public TerminationType TerminationType { get; set; }

        public DateOnly RequestDate { get; set; }

        public DateOnly ProposedLastWorkingDay { get; set; }

        public TerminationStatus Status { get; set; }

        public int ClearedItems { get; set; }

        public int TotalItems { get; set; }

        public bool HasLetter { get; set; }
    }

    /// <summary>
    /// Create / edit model for a termination request. The employee is locked
    /// after creation, and status is changed only through the validated
    /// workflow, never from this form.
    /// </summary>
    public class TerminationFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        public string? ReferenceNumber { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select an employee.")]
        public int EmployeeId { get; set; }

        public string? EmployeeName { get; set; }

        public string? EmployeeNumber { get; set; }

        public string? EmployeeDepartment { get; set; }

        public string? EmployeeDesignation { get; set; }

        public TerminationType TerminationType { get; set; } = TerminationType.Resignation;

        [Required(ErrorMessage = "Reason is required.")]
        [MaxLength(200, ErrorMessage = "Reason cannot exceed 200 characters.")]
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Explanation is required.")]
        [MaxLength(4000, ErrorMessage = "Explanation cannot exceed 4,000 characters.")]
        public string Explanation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Request date is required.")]
        [DataType(DataType.Date)]
        public DateOnly? RequestDate { get; set; }

        [Required(ErrorMessage = "Proposed last working day is required.")]
        [DataType(DataType.Date)]
        public DateOnly? ProposedLastWorkingDay { get; set; }

        [MaxLength(50, ErrorMessage = "Notice period cannot exceed 50 characters.")]
        public string? NoticePeriod { get; set; }

        [MaxLength(3000, ErrorMessage = "Remarks cannot exceed 3,000 characters.")]
        public string? Remarks { get; set; }

        public TerminationStatus Status { get; set; } = TerminationStatus.Draft;

        public bool IsEditMode => Id.HasValue;

        public List<SelectListItem> EmployeeOptions { get; set; } = new();

        public List<SelectListItem> NoticePeriodOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (RequestDate.HasValue && RequestDate.Value > today)
            {
                yield return new ValidationResult(
                    "Request date cannot be in the future.",
                    new[] { nameof(RequestDate) });
            }

            if (!IsEditMode && ProposedLastWorkingDay.HasValue && ProposedLastWorkingDay.Value < today)
            {
                yield return new ValidationResult(
                    "The proposed last working day cannot be in the past.",
                    new[] { nameof(ProposedLastWorkingDay) });
            }

            if (RequestDate.HasValue && ProposedLastWorkingDay.HasValue
                && ProposedLastWorkingDay.Value < RequestDate.Value)
            {
                yield return new ValidationResult(
                    "The proposed last working day cannot be before the request date.",
                    new[] { nameof(ProposedLastWorkingDay) });
            }
        }
    }

    /// <summary>
    /// Create / edit model for one clearance item on a termination request.
    /// </summary>
    public class ClearanceItemFormViewModel : IValidatableObject
    {
        public int? Id { get; set; }

        public int TerminationRequestId { get; set; }

        public string? ReferenceNumber { get; set; }

        public string? EmployeeName { get; set; }

        public ClearanceCategory Category { get; set; } = ClearanceCategory.ITAssets;

        [MaxLength(120, ErrorMessage = "Responsible person cannot exceed 120 characters.")]
        [RegularExpression(HrValidationRules.PersonNamePattern, ErrorMessage = HrValidationRules.PersonNameMessage)]
        public string? ResponsiblePerson { get; set; }

        [MaxLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
        public string? ResponsibleDepartment { get; set; }

        public bool IsMandatory { get; set; } = true;

        public ClearanceStatus Status { get; set; } = ClearanceStatus.Pending;

        [MaxLength(2000, ErrorMessage = "Outstanding items cannot exceed 2,000 characters.")]
        public string? OutstandingItems { get; set; }

        [MaxLength(2000, ErrorMessage = "Remarks cannot exceed 2,000 characters.")]
        public string? Remarks { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? CompletedDate { get; set; }

        public bool IsEditMode => Id.HasValue;

        public List<SelectListItem> DepartmentOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (CompletedDate.HasValue && CompletedDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult(
                    "The completion date cannot be in the future.",
                    new[] { nameof(CompletedDate) });
            }
        }
    }

    /// <summary>
    /// Full termination details page: the request, employee, approval / audit
    /// history, letter, supporting documents, and the clearance checklist with
    /// its progress.
    /// </summary>
    public class TerminationDetailsViewModel
    {
        public TerminationRequest Request { get; set; } = null!;

        public Employee Employee { get; set; } = null!;

        public string Initials { get; set; } = string.Empty;

        public bool CanManage { get; set; }

        public List<TerminationStatus> AllowedTransitions { get; set; } = new();

        public List<TerminationDocument> Documents { get; set; } = new();

        public TerminationDocument? Letter { get; set; }

        public List<ClearanceItem> ClearanceItems { get; set; } = new();

        public int ClearedCount { get; set; }

        public int TotalCount { get; set; }

        public int OutstandingMandatoryCount { get; set; }

        public bool ClearanceComplete { get; set; }

        public List<TerminationStatusHistory> StatusHistory { get; set; } = new();

        public DateOnly Today { get; set; }
    }
}
