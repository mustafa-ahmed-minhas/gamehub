using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    /// <summary>
    /// Screen 5 — Performance &amp; Warnings. A single hub that presents the
    /// people-management workload (cases, meetings, warnings, and improvement
    /// plans), plus the create / edit forms and a case details page that
    /// preserves the full history and drives the workflow.
    ///
    /// Workflow: Open -> UnderReview -> ImprovementPlan / Warning -> Resolved
    /// -> Closed. Every transition is validated server-side.
    /// </summary>
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class PerformanceController : Controller
    {
        private const int DefaultPageSize = 10;
        private static readonly int[] AllowedPageSizes = { 10, 25, 50 };

        /// <summary>
        /// Permitted case-status moves. The details page renders only the
        /// targets listed here for the current status.
        /// </summary>
        private static readonly Dictionary<PerformanceCaseStatus, PerformanceCaseStatus[]> Transitions = new()
        {
            [PerformanceCaseStatus.Open] = new[] { PerformanceCaseStatus.UnderReview, PerformanceCaseStatus.Closed },
            [PerformanceCaseStatus.UnderReview] = new[]
            {
                PerformanceCaseStatus.ImprovementPlan,
                PerformanceCaseStatus.Warning,
                PerformanceCaseStatus.Resolved,
                PerformanceCaseStatus.Closed
            },
            [PerformanceCaseStatus.ImprovementPlan] = new[]
            {
                PerformanceCaseStatus.UnderReview,
                PerformanceCaseStatus.Resolved,
                PerformanceCaseStatus.Closed
            },
            [PerformanceCaseStatus.Warning] = new[]
            {
                PerformanceCaseStatus.UnderReview,
                PerformanceCaseStatus.Resolved,
                PerformanceCaseStatus.Closed
            },
            [PerformanceCaseStatus.Resolved] = new[] { PerformanceCaseStatus.UnderReview, PerformanceCaseStatus.Closed },
            [PerformanceCaseStatus.Closed] = new[] { PerformanceCaseStatus.Open }
        };

        private readonly ApplicationDbContext _dbContext;
        private readonly IPerformanceNumberService _numberService;
        private readonly ILogger<PerformanceController> _logger;

        public PerformanceController(ApplicationDbContext dbContext, IPerformanceNumberService numberService, ILogger<PerformanceController> logger)
        {
            _dbContext = dbContext;
            _numberService = numberService;
            _logger = logger;
        }

        // ------------------------------------------------------------------
        // Hub
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Index(
            string? tab = null,
            string? search = null,
            int? employeeId = null,
            PerformanceCaseStatus? caseStatus = null,
            PerformanceSeverity? caseSeverity = null,
            WarningStatus? warningStatus = null,
            ImprovementPlanStatus? planStatus = null,
            string? caseSort = null,
            string? warningSort = null,
            string? planSort = null,
            int? page = null,
            int? pageSize = null)
        {
            await ApplyOverdueAsync();

            var activeTab = (tab ?? string.Empty).ToLowerInvariant() switch
            {
                "warnings" => "warnings",
                "plans" => "plans",
                _ => "cases"
            };

            var resolvedPageSize = pageSize.HasValue && AllowedPageSizes.Contains(pageSize.Value)
                ? pageSize.Value
                : DefaultPageSize;

            var model = new PerformanceIndexViewModel
            {
                ActiveTab = activeTab,
                CanManage = CanManage(),
                PageSize = resolvedPageSize,
                CaseSearch = activeTab == "cases" ? search : null,
                CaseEmployeeId = employeeId,
                CaseStatus = caseStatus,
                CaseSeverity = caseSeverity,
                CaseSortBy = string.IsNullOrWhiteSpace(caseSort) ? "newest" : caseSort,
                WarningSearch = activeTab == "warnings" ? search : null,
                WarningStatus = warningStatus,
                WarningSortBy = string.IsNullOrWhiteSpace(warningSort) ? "newest" : warningSort,
                PlanSearch = activeTab == "plans" ? search : null,
                PlanStatus = planStatus,
                PlanSortBy = string.IsNullOrWhiteSpace(planSort) ? "newest" : planSort
            };

            await PopulateSummaryAsync(model);
            await PopulateEmployeeOptionsAsync(model);

            switch (activeTab)
            {
                case "warnings":
                    await PopulateWarningsAsync(model, page ?? 1);
                    break;
                case "plans":
                    await PopulatePlansAsync(model, page ?? 1);
                    break;
                default:
                    await PopulateCasesAsync(model, page ?? 1);
                    break;
            }

            return View(model);
        }

        // ------------------------------------------------------------------
        // Case create / edit
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> CreateCase()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var model = new PerformanceCaseFormViewModel
            {
                CaseNumber = await _numberService.GenerateNextCaseNumberAsync(),
                ReportedDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            await PopulateCaseFormOptionsAsync(model);
            return View("CaseForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCase(PerformanceCaseFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            NormalizeForm(model);
            await PopulateCaseFormOptionsAsync(model);
            await FillEmployeeContextAsync(model);
            await ValidateCaseAsync(model, null);

            if (!ModelState.IsValid)
            {
                model.CaseNumber ??= await _numberService.GenerateNextCaseNumberAsync();
                return View("CaseForm", model);
            }

            var detail = new PerformanceCase
            {
                CaseNumber = await _numberService.GenerateNextCaseNumberAsync(),
                EmployeeId = model.EmployeeId,
                IssueType = model.IssueType,
                Title = model.Title,
                Description = model.Description,
                Severity = model.Severity,
                ReportedDate = model.ReportedDate!.Value,
                AssignedHrUserId = model.AssignedHrUserId,
                Status = PerformanceCaseStatus.Open,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            _dbContext.PerformanceCases.Add(detail);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to create performance case for employee {EmployeeId}.", model.EmployeeId);
                ModelState.AddModelError(string.Empty, "The case could not be saved due to a data conflict. Please try again.");
                model.CaseNumber ??= await _numberService.GenerateNextCaseNumberAsync();
                return View("CaseForm", model);
            }

            TempData.SetToast("success", "Success", $"Performance case {detail.CaseNumber} was created.");
            return RedirectToAction(nameof(Details), new { id = detail.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditCase(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var detail = await _dbContext.PerformanceCases
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (detail == null)
            {
                return NotFound();
            }

            var model = new PerformanceCaseFormViewModel
            {
                Id = detail.Id,
                CaseNumber = detail.CaseNumber,
                EmployeeId = detail.EmployeeId,
                EmployeeName = detail.Employee is null ? null : $"{detail.Employee.FirstName} {detail.Employee.LastName}".Trim(),
                EmployeeNumber = detail.Employee?.EmployeeNumber,
                EmployeeDepartment = detail.Employee?.Department,
                EmployeeDesignation = detail.Employee?.Designation,
                IssueType = detail.IssueType,
                Title = detail.Title,
                Description = detail.Description,
                Severity = detail.Severity,
                ReportedDate = detail.ReportedDate,
                AssignedHrUserId = detail.AssignedHrUserId,
                Status = detail.Status
            };

            await PopulateCaseFormOptionsAsync(model);
            return View("CaseForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCase(int id, PerformanceCaseFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            NormalizeForm(model);
            await PopulateCaseFormOptionsAsync(model);
            await FillEmployeeContextAsync(model);
            await ValidateCaseAsync(model, id);

            var detail = await _dbContext.PerformanceCases.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (detail == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("CaseForm", model);
            }

            detail.EmployeeId = model.EmployeeId;
            detail.IssueType = model.IssueType;
            detail.Title = model.Title;
            detail.Description = model.Description;
            detail.Severity = model.Severity;
            detail.ReportedDate = model.ReportedDate!.Value;
            detail.AssignedHrUserId = model.AssignedHrUserId;
            detail.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update performance case {CaseId}.", id);
                ModelState.AddModelError(string.Empty, "The case could not be saved. Please review the values and try again.");
                return View("CaseForm", model);
            }

            TempData.SetToast("success", "Success", $"Performance case {detail.CaseNumber} was updated.");
            return RedirectToAction(nameof(Details), new { id = detail.Id });
        }

        // ------------------------------------------------------------------
        // Details + workflow
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            await ApplyOverdueAsync();

            var detail = await LoadCaseDetailsAsync(id);
            if (detail == null)
            {
                return NotFound();
            }

            var model = new PerformanceCaseDetailsViewModel
            {
                Case = detail,
                Employee = detail.Employee!,
                Initials = Initials(detail.Employee!.FirstName, detail.Employee.LastName),
                AssignedHrName = detail.AssignedHrUser is null
                    ? null
                    : $"{detail.AssignedHrUser.FirstName} {detail.AssignedHrUser.LastName}".Trim(),
                CanManage = CanManage(),
                AllowedTransitions = AllowedTransitions(detail.Status),
                Today = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Transition(int id, PerformanceCaseStatus status, string? note = null, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var detail = await _dbContext.PerformanceCases.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (detail == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData.SetToast("warning", "Not Allowed", "Invalid case status.");
                return SafeRedirect(returnUrl, id);
            }

            if (!Transitions.TryGetValue(detail.Status, out var allowed) || !allowed.Contains(status))
            {
                TempData.SetToast("warning", "Not Allowed", $"A case cannot move from {detail.Status.GetDisplayName()} to {status.GetDisplayName()}.");
                return SafeRedirect(returnUrl, id);
            }

            detail.Status = status;
            detail.UpdatedAt = DateTime.UtcNow;

            var trimmedNote = TrimOptional(note);
            switch (status)
            {
                case PerformanceCaseStatus.Resolved:
                    detail.ResolvedAt = DateTime.UtcNow;
                    if (trimmedNote != null) detail.ResolutionNotes = trimmedNote;
                    break;
                case PerformanceCaseStatus.Closed:
                    detail.ClosedAt = DateTime.UtcNow;
                    if (trimmedNote != null) detail.ResolutionNotes = trimmedNote;
                    break;
                case PerformanceCaseStatus.Open:
                    // Reopened: clear the terminal timestamps.
                    detail.ResolvedAt = null;
                    detail.ClosedAt = null;
                    break;
            }

            if (await TrySaveAsync($"transition case {id}"))
            {
                TempData.SetToast("success", "Success", $"Case {detail.CaseNumber} is now {status.GetDisplayName()}.");
            }

            return SafeRedirect(returnUrl, id);
        }

        // ------------------------------------------------------------------
        // Meetings
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> CreateMeeting(int caseId)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var detail = await LoadCaseAsync(caseId);
            if (detail == null) return NotFound();

            var model = new PerformanceMeetingFormViewModel
            {
                PerformanceCaseId = detail.Id,
                CaseNumber = detail.CaseNumber,
                EmployeeName = CaseEmployeeName(detail),
                MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            return View("MeetingForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMeeting(PerformanceMeetingFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            await FillMeetingContextAsync(model);
            await ValidateMeetingAsync(model);

            if (!ModelState.IsValid)
            {
                return View("MeetingForm", model);
            }

            var meeting = new PerformanceMeeting
            {
                PerformanceCaseId = model.PerformanceCaseId,
                MeetingDate = model.MeetingDate!.Value,
                MeetingTime = model.MeetingTime!.Value,
                DiscussionNotes = model.DiscussionNotes,
                Outcome = TrimOptional(model.Outcome),
                FollowUpDate = model.FollowUpDate,
                Status = model.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            _dbContext.PerformanceMeetings.Add(meeting);

            // Adding a meeting keeps an in-flight case moving through review.
            await AdvanceCaseForActivityAsync(model.PerformanceCaseId, PerformanceCaseStatus.UnderReview);

            if (await TrySaveAsync($"create meeting for case {model.PerformanceCaseId}"))
            {
                TempData.SetToast("success", "Success", "The meeting record was added.");
            }

            return RedirectToAction(nameof(Details), new { id = model.PerformanceCaseId });
        }

        [HttpGet]
        public async Task<IActionResult> EditMeeting(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var meeting = await _dbContext.PerformanceMeetings
                .AsNoTracking()
                .Include(entity => entity.PerformanceCase)
                .ThenInclude(detail => detail!.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (meeting == null || meeting.PerformanceCase == null)
            {
                return NotFound();
            }

            var model = new PerformanceMeetingFormViewModel
            {
                Id = meeting.Id,
                PerformanceCaseId = meeting.PerformanceCaseId,
                CaseNumber = meeting.PerformanceCase.CaseNumber,
                EmployeeName = CaseEmployeeName(meeting.PerformanceCase),
                MeetingDate = meeting.MeetingDate,
                MeetingTime = meeting.MeetingTime,
                DiscussionNotes = meeting.DiscussionNotes,
                Outcome = meeting.Outcome,
                FollowUpDate = meeting.FollowUpDate,
                Status = meeting.Status
            };

            return View("MeetingForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMeeting(int id, PerformanceMeetingFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            await FillMeetingContextAsync(model);
            await ValidateMeetingAsync(model, id);

            var meeting = await _dbContext.PerformanceMeetings.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (meeting == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("MeetingForm", model);
            }

            meeting.MeetingDate = model.MeetingDate!.Value;
            meeting.MeetingTime = model.MeetingTime!.Value;
            meeting.DiscussionNotes = model.DiscussionNotes;
            meeting.Outcome = TrimOptional(model.Outcome);
            meeting.FollowUpDate = model.FollowUpDate;
            meeting.Status = model.Status;
            meeting.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update performance meeting {MeetingId}.", id);
                ModelState.AddModelError(string.Empty, "The meeting record could not be saved. Please try again.");
                return View("MeetingForm", model);
            }

            TempData.SetToast("success", "Success", "The meeting record was updated.");
            return RedirectToAction(nameof(Details), new { id = meeting.PerformanceCaseId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMeeting(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var meeting = await _dbContext.PerformanceMeetings.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (meeting == null)
            {
                return NotFound();
            }

            var caseId = meeting.PerformanceCaseId;
            meeting.IsActive = false;
            meeting.UpdatedAt = DateTime.UtcNow;

            if (await TrySaveAsync($"delete meeting {id}"))
            {
                TempData.SetToast("success", "Success", "The meeting record was removed.");
            }

            return SafeRedirect(returnUrl, caseId);
        }

        // ------------------------------------------------------------------
        // Warnings
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> CreateWarning(int caseId)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var detail = await LoadCaseAsync(caseId);
            if (detail == null) return NotFound();

            var model = new PerformanceWarningFormViewModel
            {
                PerformanceCaseId = detail.Id,
                CaseNumber = detail.CaseNumber,
                EmployeeName = CaseEmployeeName(detail),
                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            return View("WarningForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWarning(PerformanceWarningFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            await FillWarningContextAsync(model);
            await ValidateWarningAsync(model, null);

            if (!ModelState.IsValid)
            {
                return View("WarningForm", model);
            }

            var warning = new PerformanceWarning
            {
                PerformanceCaseId = model.PerformanceCaseId,
                Reason = model.Reason,
                IssueDate = model.IssueDate!.Value,
                ResponseRemarks = TrimOptional(model.ResponseRemarks),
                Status = model.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            _dbContext.PerformanceWarnings.Add(warning);

            // A formal warning moves an open / under-review case into the Warning stage.
            await AdvanceCaseForActivityAsync(model.PerformanceCaseId, PerformanceCaseStatus.Warning);

            if (await TrySaveAsync($"create warning for case {model.PerformanceCaseId}"))
            {
                TempData.SetToast("success", "Success", "The formal warning was recorded.");
            }

            return RedirectToAction(nameof(Details), new { id = model.PerformanceCaseId });
        }

        [HttpGet]
        public async Task<IActionResult> EditWarning(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var warning = await _dbContext.PerformanceWarnings
                .AsNoTracking()
                .Include(entity => entity.PerformanceCase)
                .ThenInclude(detail => detail!.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (warning == null || warning.PerformanceCase == null)
            {
                return NotFound();
            }

            var model = new PerformanceWarningFormViewModel
            {
                Id = warning.Id,
                PerformanceCaseId = warning.PerformanceCaseId,
                CaseNumber = warning.PerformanceCase.CaseNumber,
                EmployeeName = CaseEmployeeName(warning.PerformanceCase),
                Reason = warning.Reason,
                IssueDate = warning.IssueDate,
                ResponseRemarks = warning.ResponseRemarks,
                Status = warning.Status
            };

            return View("WarningForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWarning(int id, PerformanceWarningFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            await FillWarningContextAsync(model);
            await ValidateWarningAsync(model, id);

            var warning = await _dbContext.PerformanceWarnings.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (warning == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("WarningForm", model);
            }

            warning.Reason = model.Reason;
            warning.IssueDate = model.IssueDate!.Value;
            warning.ResponseRemarks = TrimOptional(model.ResponseRemarks);
            warning.Status = model.Status;
            warning.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update performance warning {WarningId}.", id);
                ModelState.AddModelError(string.Empty, "The formal warning could not be saved. Please try again.");
                return View("WarningForm", model);
            }

            TempData.SetToast("success", "Success", "The formal warning was updated.");
            return RedirectToAction(nameof(Details), new { id = warning.PerformanceCaseId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWarning(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var warning = await _dbContext.PerformanceWarnings.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (warning == null)
            {
                return NotFound();
            }

            var caseId = warning.PerformanceCaseId;
            warning.IsActive = false;
            warning.UpdatedAt = DateTime.UtcNow;

            if (await TrySaveAsync($"delete warning {id}"))
            {
                TempData.SetToast("success", "Success", "The formal warning was removed.");
            }

            return SafeRedirect(returnUrl, caseId);
        }

        // ------------------------------------------------------------------
        // Improvement plans
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> CreatePlan(int caseId)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var detail = await LoadCaseAsync(caseId);
            if (detail == null) return NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var model = new ImprovementPlanFormViewModel
            {
                PerformanceCaseId = detail.Id,
                CaseNumber = detail.CaseNumber,
                EmployeeName = CaseEmployeeName(detail),
                StartDate = today,
                ReviewDate = today.AddDays(30),
                EndDate = today.AddDays(60)
            };

            return View("PlanForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePlan(ImprovementPlanFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            await FillPlanContextAsync(model);
            await ValidatePlanAsync(model, null);

            if (!ModelState.IsValid)
            {
                return View("PlanForm", model);
            }

            var plan = new ImprovementPlan
            {
                PerformanceCaseId = model.PerformanceCaseId,
                Objectives = model.Objectives,
                MeasurableExpectations = model.MeasurableExpectations,
                StartDate = model.StartDate!.Value,
                ReviewDate = model.ReviewDate!.Value,
                EndDate = model.EndDate!.Value,
                ProgressPercent = model.ProgressPercent,
                ReviewNotes = TrimOptional(model.ReviewNotes),
                Outcome = TrimOptional(model.Outcome),
                Status = model.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            _dbContext.ImprovementPlans.Add(plan);

            // A plan moves an open / under-review case into the Improvement Plan stage.
            await AdvanceCaseForActivityAsync(model.PerformanceCaseId, PerformanceCaseStatus.ImprovementPlan);

            if (await TrySaveAsync($"create improvement plan for case {model.PerformanceCaseId}"))
            {
                TempData.SetToast("success", "Success", "The improvement plan was added.");
            }

            return RedirectToAction(nameof(Details), new { id = model.PerformanceCaseId });
        }

        [HttpGet]
        public async Task<IActionResult> EditPlan(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var plan = await _dbContext.ImprovementPlans
                .AsNoTracking()
                .Include(entity => entity.PerformanceCase)
                .ThenInclude(detail => detail!.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (plan == null || plan.PerformanceCase == null)
            {
                return NotFound();
            }

            var model = new ImprovementPlanFormViewModel
            {
                Id = plan.Id,
                PerformanceCaseId = plan.PerformanceCaseId,
                CaseNumber = plan.PerformanceCase.CaseNumber,
                EmployeeName = CaseEmployeeName(plan.PerformanceCase),
                Objectives = plan.Objectives,
                MeasurableExpectations = plan.MeasurableExpectations,
                StartDate = plan.StartDate,
                ReviewDate = plan.ReviewDate,
                EndDate = plan.EndDate,
                ProgressPercent = plan.ProgressPercent,
                ReviewNotes = plan.ReviewNotes,
                Outcome = plan.Outcome,
                Status = plan.Status
            };

            return View("PlanForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPlan(int id, ImprovementPlanFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            await FillPlanContextAsync(model);
            await ValidatePlanAsync(model, id);

            var plan = await _dbContext.ImprovementPlans.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (plan == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("PlanForm", model);
            }

            plan.Objectives = model.Objectives;
            plan.MeasurableExpectations = model.MeasurableExpectations;
            plan.StartDate = model.StartDate!.Value;
            plan.ReviewDate = model.ReviewDate!.Value;
            plan.EndDate = model.EndDate!.Value;
            plan.ProgressPercent = model.ProgressPercent;
            plan.ReviewNotes = TrimOptional(model.ReviewNotes);
            plan.Outcome = TrimOptional(model.Outcome);
            plan.Status = model.Status;
            plan.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update improvement plan {PlanId}.", id);
                ModelState.AddModelError(string.Empty, "The improvement plan could not be saved. Please try again.");
                return View("PlanForm", model);
            }

            TempData.SetToast("success", "Success", "The improvement plan was updated.");
            return RedirectToAction(nameof(Details), new { id = plan.PerformanceCaseId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlan(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var plan = await _dbContext.ImprovementPlans.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (plan == null)
            {
                return NotFound();
            }

            var caseId = plan.PerformanceCaseId;
            plan.IsActive = false;
            plan.UpdatedAt = DateTime.UtcNow;

            if (await TrySaveAsync($"delete improvement plan {id}"))
            {
                TempData.SetToast("success", "Success", "The improvement plan was removed.");
            }

            return SafeRedirect(returnUrl, caseId);
        }

        // ------------------------------------------------------------------
        // Data loading
        // ------------------------------------------------------------------

        private async Task PopulateSummaryAsync(PerformanceIndexViewModel model)
        {
            model.TotalCases = await _dbContext.PerformanceCases.AsNoTracking().CountAsync(detail => detail.IsActive);
            model.OpenCases = await _dbContext.PerformanceCases.AsNoTracking()
                .CountAsync(detail => detail.IsActive
                    && (detail.Status == PerformanceCaseStatus.Open || detail.Status == PerformanceCaseStatus.UnderReview));
            model.MeetingsScheduled = await _dbContext.PerformanceMeetings.AsNoTracking()
                .CountAsync(meeting => meeting.IsActive && meeting.Status == PerformanceMeetingStatus.Scheduled);
            model.ActiveWarnings = await _dbContext.PerformanceWarnings.AsNoTracking()
                .CountAsync(warning => warning.IsActive
                    && (warning.Status == WarningStatus.Issued
                        || warning.Status == WarningStatus.Acknowledged
                        || warning.Status == WarningStatus.Contested
                        || warning.Status == WarningStatus.Escalated));
            model.ImprovementPlans = await _dbContext.ImprovementPlans.AsNoTracking()
                .CountAsync(plan => plan.IsActive
                    && (plan.Status == ImprovementPlanStatus.Active
                        || plan.Status == ImprovementPlanStatus.UnderReview
                        || plan.Status == ImprovementPlanStatus.Overdue));
            model.ResolvedCases = await _dbContext.PerformanceCases.AsNoTracking()
                .CountAsync(detail => detail.IsActive
                    && (detail.Status == PerformanceCaseStatus.Resolved || detail.Status == PerformanceCaseStatus.Closed));
        }

        private async Task PopulateEmployeeOptionsAsync(PerformanceIndexViewModel model)
        {
            model.EmployeeOptions = await _dbContext.Employees.AsNoTracking()
                .Where(employee => employee.IsActive && employee.PerformanceCases.Any(detail => detail.IsActive))
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new SelectListItem
                {
                    Value = employee.Id.ToString(),
                    Text = employee.FirstName + " " + employee.LastName + " (" + employee.EmployeeNumber + ")"
                })
                .ToListAsync();
        }

        private async Task PopulateCasesAsync(PerformanceIndexViewModel model, int page)
        {
            var query = _dbContext.PerformanceCases.AsNoTracking().Where(detail => detail.IsActive);

            if (!string.IsNullOrWhiteSpace(model.CaseSearch))
            {
                var term = model.CaseSearch.Trim();
                query = query.Where(detail =>
                    detail.CaseNumber.Contains(term)
                    || detail.Title.Contains(term)
                    || detail.Employee!.EmployeeNumber.Contains(term)
                    || detail.Employee!.FirstName.Contains(term)
                    || detail.Employee!.LastName.Contains(term)
                    || (detail.Employee!.FirstName + " " + detail.Employee!.LastName).Contains(term));
            }

            if (model.CaseEmployeeId.HasValue)
            {
                query = query.Where(detail => detail.EmployeeId == model.CaseEmployeeId.Value);
            }

            if (model.CaseStatus.HasValue)
            {
                query = query.Where(detail => detail.Status == model.CaseStatus.Value);
            }

            if (model.CaseSeverity.HasValue)
            {
                query = query.Where(detail => detail.Severity == model.CaseSeverity.Value);
            }

            query = model.CaseSortBy switch
            {
                "oldest" => query.OrderBy(detail => detail.CreatedAt),
                "employee" => query.OrderBy(detail => detail.Employee!.FirstName).ThenBy(detail => detail.Employee!.LastName),
                "reported" => query.OrderByDescending(detail => detail.ReportedDate),
                "severity" => query.OrderByDescending(detail => detail.Severity),
                _ => query.OrderByDescending(detail => detail.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Cases = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(detail => new PerformanceCaseListItemViewModel
                {
                    Id = detail.Id,
                    CaseNumber = detail.CaseNumber,
                    Title = detail.Title,
                    EmployeeId = detail.EmployeeId,
                    EmployeeName = detail.Employee!.FirstName + " " + detail.Employee!.LastName,
                    EmployeeNumber = detail.Employee!.EmployeeNumber,
                    Department = detail.Employee!.Department,
                    Designation = detail.Employee!.Designation,
                    IssueType = detail.IssueType,
                    Severity = detail.Severity,
                    Status = detail.Status,
                    ReportedDate = detail.ReportedDate,
                    AssignedHrName = detail.AssignedHrUser == null
                        ? null
                        : detail.AssignedHrUser.FirstName + " " + detail.AssignedHrUser.LastName,
                    MeetingCount = detail.Meetings.Count(meeting => meeting.IsActive),
                    WarningCount = detail.Warnings.Count(warning => warning.IsActive),
                    PlanCount = detail.ImprovementPlans.Count(plan => plan.IsActive)
                })
                .ToListAsync();

            foreach (var item in model.Cases)
            {
                item.Initials = Initials(item.EmployeeName);
            }
        }

        private async Task PopulateWarningsAsync(PerformanceIndexViewModel model, int page)
        {
            var query = _dbContext.PerformanceWarnings.AsNoTracking().Where(warning => warning.IsActive);

            if (!string.IsNullOrWhiteSpace(model.WarningSearch))
            {
                var term = model.WarningSearch.Trim();
                query = query.Where(warning =>
                    warning.PerformanceCase!.CaseNumber.Contains(term)
                    || warning.Reason.Contains(term)
                    || warning.PerformanceCase!.Employee!.FirstName.Contains(term)
                    || warning.PerformanceCase!.Employee!.LastName.Contains(term)
                    || (warning.PerformanceCase!.Employee!.FirstName + " " + warning.PerformanceCase!.Employee!.LastName).Contains(term));
            }

            if (model.WarningStatus.HasValue)
            {
                query = query.Where(warning => warning.Status == model.WarningStatus.Value);
            }

            query = model.WarningSortBy switch
            {
                "oldest" => query.OrderBy(warning => warning.CreatedAt),
                "issue" => query.OrderByDescending(warning => warning.IssueDate),
                _ => query.OrderByDescending(warning => warning.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Warnings = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(warning => new PerformanceWarningListItemViewModel
                {
                    Id = warning.Id,
                    PerformanceCaseId = warning.PerformanceCaseId,
                    CaseNumber = warning.PerformanceCase!.CaseNumber,
                    CaseTitle = warning.PerformanceCase!.Title,
                    EmployeeName = warning.PerformanceCase!.Employee!.FirstName + " " + warning.PerformanceCase!.Employee!.LastName,
                    Reason = warning.Reason,
                    IssueDate = warning.IssueDate,
                    Status = warning.Status
                })
                .ToListAsync();

            foreach (var item in model.Warnings)
            {
                item.Initials = Initials(item.EmployeeName);
            }
        }

        private async Task PopulatePlansAsync(PerformanceIndexViewModel model, int page)
        {
            var query = _dbContext.ImprovementPlans.AsNoTracking().Where(plan => plan.IsActive);

            if (!string.IsNullOrWhiteSpace(model.PlanSearch))
            {
                var term = model.PlanSearch.Trim();
                query = query.Where(plan =>
                    plan.PerformanceCase!.CaseNumber.Contains(term)
                    || plan.Objectives.Contains(term)
                    || plan.PerformanceCase!.Employee!.FirstName.Contains(term)
                    || plan.PerformanceCase!.Employee!.LastName.Contains(term)
                    || (plan.PerformanceCase!.Employee!.FirstName + " " + plan.PerformanceCase!.Employee!.LastName).Contains(term));
            }

            if (model.PlanStatus.HasValue)
            {
                query = query.Where(plan => plan.Status == model.PlanStatus.Value);
            }

            query = model.PlanSortBy switch
            {
                "oldest" => query.OrderBy(plan => plan.CreatedAt),
                "review" => query.OrderBy(plan => plan.ReviewDate),
                "end" => query.OrderBy(plan => plan.EndDate),
                _ => query.OrderByDescending(plan => plan.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Plans = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(plan => new ImprovementPlanListItemViewModel
                {
                    Id = plan.Id,
                    PerformanceCaseId = plan.PerformanceCaseId,
                    CaseNumber = plan.PerformanceCase!.CaseNumber,
                    EmployeeName = plan.PerformanceCase!.Employee!.FirstName + " " + plan.PerformanceCase!.Employee!.LastName,
                    Objectives = plan.Objectives,
                    StartDate = plan.StartDate,
                    ReviewDate = plan.ReviewDate,
                    EndDate = plan.EndDate,
                    ProgressPercent = plan.ProgressPercent,
                    Status = plan.Status
                })
                .ToListAsync();

            foreach (var item in model.Plans)
            {
                item.Initials = Initials(item.EmployeeName);
            }
        }

        private async Task PopulateCaseFormOptionsAsync(PerformanceCaseFormViewModel model)
        {
            model.EmployeeOptions = await _dbContext.Employees.AsNoTracking()
                .Where(employee => employee.IsActive)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new SelectListItem
                {
                    Value = employee.Id.ToString(),
                    Text = employee.FirstName + " " + employee.LastName + " (" + employee.EmployeeNumber + ")",
                    Selected = employee.Id == model.EmployeeId
                })
                .ToListAsync();

            model.HrUserOptions = await _dbContext.Users.AsNoTracking()
                .Where(user => user.IsActive
                    && (user.Role == UserRole.SuperAdmin || user.Role == UserRole.Admin || user.Role == UserRole.StaffManager))
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new SelectListItem
                {
                    Value = user.Id.ToString(),
                    Text = user.FirstName + " " + user.LastName,
                    Selected = user.Id == model.AssignedHrUserId
                })
                .ToListAsync();
        }

        private async Task FillEmployeeContextAsync(PerformanceCaseFormViewModel model)
        {
            if (model.EmployeeId <= 0)
            {
                return;
            }

            var employee = await _dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == model.EmployeeId);

            if (employee == null)
            {
                return;
            }

            model.EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim();
            model.EmployeeNumber = employee.EmployeeNumber;
            model.EmployeeDepartment = employee.Department;
            model.EmployeeDesignation = employee.Designation;
        }

        private async Task FillMeetingContextAsync(PerformanceMeetingFormViewModel model)
        {
            var detail = await _dbContext.PerformanceCases
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == model.PerformanceCaseId && entity.IsActive);

            model.CaseNumber = detail?.CaseNumber;
            model.EmployeeName = CaseEmployeeName(detail);
        }

        private async Task FillWarningContextAsync(PerformanceWarningFormViewModel model)
        {
            var detail = await _dbContext.PerformanceCases
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == model.PerformanceCaseId && entity.IsActive);

            model.CaseNumber = detail?.CaseNumber;
            model.EmployeeName = CaseEmployeeName(detail);
        }

        private async Task FillPlanContextAsync(ImprovementPlanFormViewModel model)
        {
            var detail = await _dbContext.PerformanceCases
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == model.PerformanceCaseId && entity.IsActive);

            model.CaseNumber = detail?.CaseNumber;
            model.EmployeeName = CaseEmployeeName(detail);
        }

        private async Task ValidateCaseAsync(PerformanceCaseFormViewModel model, int? excludeId)
        {
            if (model.EmployeeId <= 0)
            {
                return;
            }

            var employeeExists = await _dbContext.Employees.AsNoTracking()
                .AnyAsync(employee => employee.Id == model.EmployeeId && employee.IsActive);
            if (!employeeExists)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "The selected employee could not be found.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.Title))
            {
                var title = model.Title.Trim();
                var duplicate = await _dbContext.PerformanceCases.AsNoTracking().AnyAsync(detail =>
                    detail.IsActive
                    && detail.EmployeeId == model.EmployeeId
                    && detail.Title == title
                    && detail.Status != PerformanceCaseStatus.Closed
                    && (excludeId == null || detail.Id != excludeId.Value));
                if (duplicate)
                {
                    ModelState.AddModelError(nameof(model.Title),
                        "An open case with this title already exists for the selected employee.");
                }
            }

            if (model.AssignedHrUserId.HasValue)
            {
                var hrUserIsValid = await _dbContext.Users.AsNoTracking().AnyAsync(user =>
                    user.Id == model.AssignedHrUserId.Value
                    && user.IsActive
                    && (user.Role == UserRole.SuperAdmin || user.Role == UserRole.Admin || user.Role == UserRole.StaffManager));

                if (!hrUserIsValid)
                {
                    ModelState.AddModelError(nameof(model.AssignedHrUserId), "Select an active HR user for this case.");
                }
            }
        }

        private async Task ValidateMeetingAsync(PerformanceMeetingFormViewModel model, int? excludeId = null)
        {
            var exists = await _dbContext.PerformanceCases.AsNoTracking()
                .AnyAsync(detail => detail.Id == model.PerformanceCaseId && detail.IsActive);
            if (!exists)
            {
                ModelState.AddModelError(nameof(model.PerformanceCaseId), "The linked case could not be found.");
                return;
            }

            if (model.MeetingDate.HasValue && model.MeetingTime.HasValue)
            {
                var slotTaken = await _dbContext.PerformanceMeetings.AsNoTracking().AnyAsync(meeting =>
                    meeting.IsActive
                    && meeting.PerformanceCaseId == model.PerformanceCaseId
                    && meeting.MeetingDate == model.MeetingDate.Value
                    && meeting.MeetingTime == model.MeetingTime.Value
                    && meeting.Status != PerformanceMeetingStatus.Cancelled
                    && (excludeId == null || meeting.Id != excludeId.Value));

                if (slotTaken)
                {
                    ModelState.AddModelError(nameof(model.MeetingTime),
                        "A meeting for this case is already scheduled at that date and time.");
                }
            }
        }

        private async Task ValidateWarningAsync(PerformanceWarningFormViewModel model, int? excludeId)
        {
            var exists = await _dbContext.PerformanceCases.AsNoTracking()
                .AnyAsync(detail => detail.Id == model.PerformanceCaseId && detail.IsActive);
            if (!exists)
            {
                ModelState.AddModelError(nameof(model.PerformanceCaseId), "The linked case could not be found.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.Reason) && model.IssueDate.HasValue)
            {
                var reason = model.Reason.Trim();
                var issueDate = model.IssueDate.Value;
                var duplicate = await _dbContext.PerformanceWarnings.AsNoTracking().AnyAsync(warning =>
                    warning.IsActive
                    && warning.PerformanceCaseId == model.PerformanceCaseId
                    && warning.Reason == reason
                    && warning.IssueDate == issueDate
                    && (excludeId == null || warning.Id != excludeId.Value));
                if (duplicate)
                {
                    ModelState.AddModelError(nameof(model.Reason), "An identical warning already exists on this case.");
                }
            }
        }

        private async Task ValidatePlanAsync(ImprovementPlanFormViewModel model, int? excludeId)
        {
            var exists = await _dbContext.PerformanceCases.AsNoTracking()
                .AnyAsync(detail => detail.Id == model.PerformanceCaseId && detail.IsActive);
            if (!exists)
            {
                ModelState.AddModelError(nameof(model.PerformanceCaseId), "The linked case could not be found.");
                return;
            }

            // Keep one in-flight plan per case so duplicate submissions cannot
            // pile up active plans.
            var hasActivePlan = await _dbContext.ImprovementPlans.AsNoTracking().AnyAsync(plan =>
                plan.IsActive
                && plan.PerformanceCaseId == model.PerformanceCaseId
                && (plan.Status == ImprovementPlanStatus.Active || plan.Status == ImprovementPlanStatus.UnderReview || plan.Status == ImprovementPlanStatus.Overdue)
                && (excludeId == null || plan.Id != excludeId.Value));
            if (hasActivePlan)
            {
                ModelState.AddModelError(string.Empty,
                    "This case already has an active improvement plan. Complete or cancel it before adding another.");
            }
        }

        /// <summary>
        /// Moves a case forward when a meeting / warning / plan is added. Only
        /// in-flight cases advance; resolved, closed, or more advanced cases are
        /// left untouched so the workflow is never silently rolled back.
        /// </summary>
        private async Task AdvanceCaseForActivityAsync(int caseId, PerformanceCaseStatus target)
        {
            var detail = await _dbContext.PerformanceCases.FirstOrDefaultAsync(entity => entity.Id == caseId && entity.IsActive);
            if (detail == null)
            {
                return;
            }

            if (detail.Status is PerformanceCaseStatus.Open or PerformanceCaseStatus.UnderReview)
            {
                detail.Status = target;
                detail.UpdatedAt = DateTime.UtcNow;
            }
        }

        private Task ApplyOverdueAsync() => ApplyOverdueAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        private async Task ApplyOverdueAsync(DateOnly today)
        {
            var overdue = await _dbContext.ImprovementPlans
                .Where(plan => plan.IsActive
                    && plan.Status == ImprovementPlanStatus.Active
                    && plan.EndDate < today)
                .ToListAsync();

            if (overdue.Count == 0)
            {
                return;
            }

            foreach (var plan in overdue)
            {
                plan.Status = ImprovementPlanStatus.Overdue;
                plan.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to mark {Count} improvement plan(s) overdue.", overdue.Count);
            }
        }

        private async Task<PerformanceCase?> LoadCaseAsync(int id) =>
            await _dbContext.PerformanceCases
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

        private async Task<PerformanceCase?> LoadCaseDetailsAsync(int id) =>
            await _dbContext.PerformanceCases
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .Include(entity => entity.AssignedHrUser)
                .Include(entity => entity.CreatedByUser)
                .Include(entity => entity.Meetings.Where(meeting => meeting.IsActive))
                .Include(entity => entity.Warnings.Where(warning => warning.IsActive))
                .Include(entity => entity.ImprovementPlans.Where(plan => plan.IsActive))
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

        private static List<PerformanceCaseStatus> AllowedTransitions(PerformanceCaseStatus status) =>
            Transitions.TryGetValue(status, out var targets) ? targets.ToList() : new List<PerformanceCaseStatus>();

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void NormalizeForm(PerformanceCaseFormViewModel model)
        {
            model.Title = TrimRequired(model.Title);
            model.Description = TrimRequired(model.Description);
        }

        private async Task<bool> TrySaveAsync(string action)
        {
            try
            {
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save performance change: {Action}.", action);
                TempData.SetToast("error", "Save Failed", "The change could not be saved. Please try again.");
                return false;
            }
        }

        private IActionResult SafeRedirect(string? returnUrl, int caseId) =>
            string.IsNullOrWhiteSpace(returnUrl)
                ? RedirectToAction(nameof(Details), new { id = caseId })
                : Redirect(returnUrl);

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private static string CaseEmployeeName(PerformanceCase? detail) =>
            detail?.Employee is null ? string.Empty : $"{detail.Employee.FirstName} {detail.Employee.LastName}".Trim();

        private static string Initials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0
                ? "NA"
                : string.Concat(parts.Take(2).Select(part => part[0])).ToUpperInvariant();
        }

        private static string Initials(string firstName, string lastName)
        {
            var first = firstName.FirstOrDefault();
            var last = lastName.FirstOrDefault();
            var initials = $"{first}{last}".Trim();
            return string.IsNullOrWhiteSpace(initials) ? "NA" : initials.ToUpperInvariant();
        }

        private static string TrimRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
