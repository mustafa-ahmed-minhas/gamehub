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
    /// Screen 6 — Termination &amp; Clearance. A single hub that presents the
    /// offboarding workload (requests, approvals, letters, and clearance), plus
    /// the create / edit forms and a details page that preserves the full
    /// history and drives the workflow.
    ///
    /// Workflow: Draft -> PendingApproval -> Approved / Rejected -> InProgress
    /// -&gt; Completed. Every transition and every clearance move is validated
    /// server-side, and a request can only be completed once all mandatory
    /// clearance items are cleared.
    /// </summary>
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class TerminationController : Controller
    {
        private const int DefaultPageSize = 10;
        private static readonly int[] AllowedPageSizes = { 10, 25, 50 };

        private const string DocumentsFolder = "terminations";
        private const int MaxSupportingDocuments = 5;

        /// <summary>The standard clearance checklist seeded on approval.</summary>
        private static readonly ClearanceCategory[] DefaultClearanceCategories =
        {
            ClearanceCategory.ITAssets,
            ClearanceCategory.DepartmentHandover,
            ClearanceCategory.Finance,
            ClearanceCategory.Administration,
            ClearanceCategory.HR
        };

        private static readonly string[] DefaultNoticePeriods =
        {
            "Immediate",
            "1 Week",
            "2 Weeks",
            "1 Month",
            "2 Months",
            "3 Months",
            "As per contract",
            "Other"
        };

        private readonly ApplicationDbContext _dbContext;
        private readonly ITerminationNumberService _numberService;
        private readonly IJobApplicationFileService _fileService;
        private readonly ISimplePdfGenerator _pdfGenerator;

        public TerminationController(
            ApplicationDbContext dbContext,
            ITerminationNumberService numberService,
            IJobApplicationFileService fileService,
            ISimplePdfGenerator pdfGenerator)
        {
            _dbContext = dbContext;
            _numberService = numberService;
            _fileService = fileService;
            _pdfGenerator = pdfGenerator;
        }

        // ------------------------------------------------------------------
        // Hub
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search = null,
            int? employeeId = null,
            TerminationStatus? status = null,
            TerminationType? terminationType = null,
            string? sort = null,
            int? page = null,
            int? pageSize = null)
        {
            var resolvedPageSize = pageSize.HasValue && AllowedPageSizes.Contains(pageSize.Value)
                ? pageSize.Value
                : DefaultPageSize;

            var model = new TerminationIndexViewModel
            {
                CanManage = CanManage(),
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                EmployeeId = employeeId,
                Status = status,
                TerminationType = terminationType,
                SortBy = string.IsNullOrWhiteSpace(sort) ? "newest" : sort,
                PageSize = resolvedPageSize
            };

            await PopulateSummaryAsync(model);
            await PopulateEmployeeOptionsAsync(model);
            await PopulateRequestsAsync(model, page ?? 1);

            return View(model);
        }

        // ------------------------------------------------------------------
        // Termination requests
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var model = new TerminationFormViewModel
            {
                ReferenceNumber = await _numberService.GenerateNextReferenceAsync(),
                RequestDate = today,
                ProposedLastWorkingDay = today.AddDays(30)
            };

            await PopulateFormOptionsAsync(model);
            return View("Form", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TerminationFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            NormalizeForm(model);
            await PopulateFormOptionsAsync(model);
            await FillEmployeeContextAsync(model);
            await ValidateRequestAsync(model, null);

            if (!ModelState.IsValid)
            {
                model.ReferenceNumber ??= await _numberService.GenerateNextReferenceAsync();
                return View("Form", model);
            }

            var request = new TerminationRequest
            {
                ReferenceNumber = await _numberService.GenerateNextReferenceAsync(),
                EmployeeId = model.EmployeeId,
                TerminationType = model.TerminationType,
                Reason = model.Reason,
                Explanation = model.Explanation,
                RequestDate = model.RequestDate!.Value,
                ProposedLastWorkingDay = model.ProposedLastWorkingDay!.Value,
                NoticePeriod = model.NoticePeriod,
                Remarks = model.Remarks,
                Status = TerminationStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            request.StatusHistory.Add(new TerminationStatusHistory
            {
                FromStatus = null,
                ToStatus = TerminationStatus.Draft,
                Note = "Request created.",
                ChangedByUserId = CurrentUserId(),
                ChangedAt = DateTime.UtcNow
            });

            _dbContext.TerminationRequests.Add(request);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "A request with this reference already exists. Please try again.");
                model.ReferenceNumber ??= await _numberService.GenerateNextReferenceAsync();
                return View("Form", model);
            }

            TempData.SetToast("success", "Success", $"Termination request {request.ReferenceNumber} was created.");
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (request == null)
            {
                return NotFound();
            }

            if (request.Status != TerminationStatus.Draft)
            {
                TempData.SetToast("warning", "Not Allowed", "Only draft requests can be edited. Submitted requests are locked.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new TerminationFormViewModel
            {
                Id = request.Id,
                ReferenceNumber = request.ReferenceNumber,
                EmployeeId = request.EmployeeId,
                EmployeeName = request.Employee is null ? null : $"{request.Employee.FirstName} {request.Employee.LastName}".Trim(),
                EmployeeNumber = request.Employee?.EmployeeNumber,
                EmployeeDepartment = request.Employee?.Department,
                EmployeeDesignation = request.Employee?.Designation,
                TerminationType = request.TerminationType,
                Reason = request.Reason,
                Explanation = request.Explanation,
                RequestDate = request.RequestDate,
                ProposedLastWorkingDay = request.ProposedLastWorkingDay,
                NoticePeriod = request.NoticePeriod,
                Remarks = request.Remarks,
                Status = request.Status
            };

            await PopulateFormOptionsAsync(model);
            return View("Form", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TerminationFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            NormalizeForm(model);
            await PopulateFormOptionsAsync(model);
            await FillEmployeeContextAsync(model);
            await ValidateRequestAsync(model, id);

            var request = await _dbContext.TerminationRequests.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            if (request.Status != TerminationStatus.Draft)
            {
                TempData.SetToast("warning", "Not Allowed", "Only draft requests can be edited. Submitted requests are locked.");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                return View("Form", model);
            }

            request.EmployeeId = model.EmployeeId;
            request.TerminationType = model.TerminationType;
            request.Reason = model.Reason;
            request.Explanation = model.Explanation;
            request.RequestDate = model.RequestDate!.Value;
            request.ProposedLastWorkingDay = model.ProposedLastWorkingDay!.Value;
            request.NoticePeriod = model.NoticePeriod;
            request.Remarks = model.Remarks;
            request.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "The request could not be saved. Please review the values and try again.");
                return View("Form", model);
            }

            TempData.SetToast("success", "Success", $"Termination request {request.ReferenceNumber} was updated.");
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }

        // ------------------------------------------------------------------
        // Details + workflow
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var request = await LoadDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            var items = request.ClearanceItems.Where(item => item.IsActive).ToList();
            var mandatoryOutstanding = items.Count(item => item.IsMandatory && item.Status != ClearanceStatus.Cleared);

            var model = new TerminationDetailsViewModel
            {
                Request = request,
                Employee = request.Employee!,
                Initials = Initials(request.Employee!.FirstName, request.Employee.LastName),
                CanManage = CanManage(),
                AllowedTransitions = TerminationWorkflow.NextStatuses(request.Status).ToList(),
                Documents = request.Documents
                    .Where(document => !document.IsLetter)
                    .OrderByDescending(document => document.Id)
                    .ToList(),
                Letter = request.Documents
                    .Where(document => document.IsLetter)
                    .OrderByDescending(document => document.Id)
                    .FirstOrDefault(),
                ClearanceItems = items.OrderBy(item => item.Category).ToList(),
                ClearedCount = items.Count(item => item.Status == ClearanceStatus.Cleared),
                TotalCount = items.Count,
                OutstandingMandatoryCount = mandatoryOutstanding,
                ClearanceComplete = items.Count > 0 && mandatoryOutstanding == 0,
                StatusHistory = request.StatusHistory
                    .OrderByDescending(history => history.ChangedAt)
                    .ThenByDescending(history => history.Id)
                    .ToList(),
                Today = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Transition(int id, TerminationStatus status, string? note = null, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests
                .Include(entity => entity.Employee)
                .Include(entity => entity.ClearanceItems)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            if (!TerminationWorkflow.CanTransition(request.Status, status))
            {
                TempData.SetToast("warning", "Not Allowed", $"A request cannot move from {request.Status.GetDisplayName()} to {status.GetDisplayName()}.");
                return SafeRedirect(returnUrl, id);
            }

            var trimmedNote = TrimOptional(note);

            if (status == TerminationStatus.Rejected && trimmedNote is null)
            {
                TempData.SetToast("warning", "Reason Required", "Provide a reason before rejecting a termination request.");
                return SafeRedirect(returnUrl, id);
            }

            if (status == TerminationStatus.Completed)
            {
                var activeItems = request.ClearanceItems.Where(item => item.IsActive).ToList();
                if (activeItems.Count == 0)
                {
                    TempData.SetToast("warning", "Clearance Required", "Create the clearance checklist before completing this termination.");
                    return SafeRedirect(returnUrl, id);
                }

                var outstanding = activeItems.Count(item => item.IsMandatory && item.Status != ClearanceStatus.Cleared);
                if (outstanding > 0)
                {
                    TempData.SetToast("warning", "Clearance Outstanding", $"{outstanding} mandatory clearance item(s) must be cleared before this termination can be completed.");
                    return SafeRedirect(returnUrl, id);
                }
            }

            var fromStatus = request.Status;
            request.Status = status;
            request.UpdatedAt = DateTime.UtcNow;

            switch (status)
            {
                case TerminationStatus.Approved:
                    request.DecisionRemarks = TrimOptional(note);
                    request.DecisionAt = DateTime.UtcNow;
                    request.DecidedByUserId = CurrentUserId();
                    SeedClearanceItems(request);
                    break;

                case TerminationStatus.Rejected:
                    request.DecisionRemarks = TrimOptional(note);
                    request.DecisionAt = DateTime.UtcNow;
                    request.DecidedByUserId = CurrentUserId();
                    break;

                case TerminationStatus.InProgress:
                    if (!request.ClearanceItems.Any(item => item.IsActive))
                    {
                        SeedClearanceItems(request);
                    }
                    break;

                case TerminationStatus.Completed:
                    request.CompletedAt = DateTime.UtcNow;
                    await MarkEmployeeTerminatedAsync(request.EmployeeId);
                    break;
            }

            request.StatusHistory.Add(new TerminationStatusHistory
            {
                FromStatus = fromStatus,
                ToStatus = status,
                Note = TrimOptional(note),
                ChangedByUserId = CurrentUserId(),
                ChangedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", $"Termination {request.ReferenceNumber} moved to {status.GetDisplayName()}.");
            return SafeRedirect(returnUrl, id);
        }

        // ------------------------------------------------------------------
        // Termination letter + supporting documents
        // ------------------------------------------------------------------

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateLetter(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests
                .Include(entity => entity.Employee)
                .Include(entity => entity.Documents)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (request?.Employee == null)
            {
                return NotFound();
            }

            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var companyName = string.IsNullOrWhiteSpace(settings?.ArenaName) ? "GameHub Arena" : settings!.ArenaName;
            var managerName = await ReportingManagerNameAsync(request.Employee.ReportingManagerId);

            var lines = TerminationLetterComposer.Compose(
                request,
                request.Employee,
                managerName,
                companyName,
                settings?.Address,
                settings?.Phone ?? string.Empty,
                settings?.Email ?? string.Empty);

            var bytes = _pdfGenerator.Generate(TerminationLetterComposer.LetterTitle, lines);

            // Replace any previously stored letter; supporting documents are untouched.
            foreach (var existing in request.Documents.Where(document => document.IsLetter).ToList())
            {
                await _fileService.DeleteAsync(existing.StoredFileName, DocumentsFolder);
                _dbContext.TerminationDocuments.Remove(existing);
            }

            var storedName = await _fileService.SaveBytesAsync(bytes, DocumentsFolder);
            _dbContext.TerminationDocuments.Add(new TerminationDocument
            {
                TerminationRequestId = request.Id,
                StoredFileName = storedName,
                OriginalFileName = $"{request.ReferenceNumber}-Termination-Letter.pdf",
                ContentType = "application/pdf",
                FileSize = bytes.Length,
                IsLetter = true,
                IsGenerated = true,
                Title = "Termination Letter",
                CreatedAt = DateTime.UtcNow
            });

            request.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The termination letter was generated.");
            return SafeRedirect(returnUrl, id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadLetter(int id, IFormFile? letter, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests
                .Include(entity => entity.Documents)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            if (letter == null || letter.Length == 0)
            {
                TempData.SetToast("warning", "No File", "Select a PDF termination letter to upload.");
                return SafeRedirect(returnUrl, id);
            }

            var result = await _fileService.SavePdfAsync(letter, DocumentsFolder);
            if (result is not { Success: true })
            {
                TempData.SetToast("error", "Upload Failed", result?.ErrorMessage ?? "The termination letter could not be uploaded.");
                return SafeRedirect(returnUrl, id);
            }

            foreach (var existing in request.Documents.Where(document => document.IsLetter).ToList())
            {
                await _fileService.DeleteAsync(existing.StoredFileName, DocumentsFolder);
                _dbContext.TerminationDocuments.Remove(existing);
            }

            _dbContext.TerminationDocuments.Add(new TerminationDocument
            {
                TerminationRequestId = request.Id,
                StoredFileName = result.StoredFileName!,
                OriginalFileName = Truncate(letter.FileName, 255),
                ContentType = "application/pdf",
                FileSize = letter.Length,
                IsLetter = true,
                IsGenerated = false,
                Title = "Termination Letter",
                CreatedAt = DateTime.UtcNow
            });

            request.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The termination letter was uploaded.");
            return SafeRedirect(returnUrl, id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocuments(int id, List<IFormFile>? documents, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            var files = (documents ?? new List<IFormFile>()).Where(file => file.Length > 0).ToList();
            if (files.Count == 0)
            {
                TempData.SetToast("warning", "No Documents", "Select at least one PDF to upload.");
                return SafeRedirect(returnUrl, id);
            }

            var existingCount = await _dbContext.TerminationDocuments
                .CountAsync(document => document.TerminationRequestId == id && !document.IsLetter);
            if (existingCount + files.Count > MaxSupportingDocuments)
            {
                TempData.SetToast("warning", "Limit Reached",
                    $"A maximum of {MaxSupportingDocuments} supporting documents is allowed per request.");
                return SafeRedirect(returnUrl, id);
            }

            var saved = new List<(string StoredName, IFormFile File)>();
            foreach (var file in files)
            {
                var result = await _fileService.SavePdfAsync(file, DocumentsFolder);
                if (result is not { Success: true })
                {
                    foreach (var (stored, _) in saved)
                    {
                        await _fileService.DeleteAsync(stored, DocumentsFolder);
                    }

                    TempData.SetToast("error", "Upload Failed", result?.ErrorMessage ?? "One of the documents could not be uploaded.");
                    return SafeRedirect(returnUrl, id);
                }

                saved.Add((result.StoredFileName!, file));
            }

            foreach (var (storedName, file) in saved)
            {
                _dbContext.TerminationDocuments.Add(new TerminationDocument
                {
                    TerminationRequestId = request.Id,
                    StoredFileName = storedName,
                    OriginalFileName = Truncate(file.FileName, 255),
                    ContentType = "application/pdf",
                    FileSize = file.Length,
                    IsLetter = false,
                    IsGenerated = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            request.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", $"{(saved.Count == 1 ? "1 document was" : $"{saved.Count} documents were")} uploaded.");
            return SafeRedirect(returnUrl, id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var document = await _dbContext.TerminationDocuments.FirstOrDefaultAsync(entity => entity.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            var requestId = document.TerminationRequestId;
            await _fileService.DeleteAsync(document.StoredFileName, DocumentsFolder);
            _dbContext.TerminationDocuments.Remove(document);
            await TouchRequestAsync(requestId);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The document was removed.");
            return SafeRedirect(returnUrl, requestId);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            if (!CanViewDocuments()) return NotFound();

            var document = await _dbContext.TerminationDocuments.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            try
            {
                var bytes = await _fileService.ReadAllBytesAsync(document.StoredFileName, DocumentsFolder);
                return File(bytes, document.ContentType ?? "application/pdf", document.OriginalFileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadLetter(int id)
        {
            var letter = await LoadLetterAsync(id);
            if (letter == null) return NotFound();

            try
            {
                var bytes = await _fileService.ReadAllBytesAsync(letter.StoredFileName, DocumentsFolder);
                return File(bytes, "application/pdf", letter.OriginalFileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> PreviewLetter(int id)
        {
            var letter = await LoadLetterAsync(id);
            if (letter == null) return NotFound();

            try
            {
                var bytes = await _fileService.ReadAllBytesAsync(letter.StoredFileName, DocumentsFolder);
                return File(bytes, "application/pdf");
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        // ------------------------------------------------------------------
        // Clearance checklist
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> CreateClearance(int requestId)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == requestId && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(request.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "Clearance items can only be recorded for approved or in-progress requests.");
                return RedirectToAction(nameof(Details), new { id = requestId });
            }

            var model = new ClearanceItemFormViewModel
            {
                TerminationRequestId = request.Id,
                ReferenceNumber = request.ReferenceNumber,
                EmployeeName = request.Employee is null ? null : $"{request.Employee.FirstName} {request.Employee.LastName}".Trim(),
                ResponsibleDepartment = request.Employee?.Department,
                Status = ClearanceStatus.Pending
            };

            PopulateClearanceOptions(model);
            return View("ClearanceForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClearance(ClearanceItemFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            NormalizeClearance(model);

            var request = await _dbContext.TerminationRequests
                .AsNoTracking()
                .Include(entity => entity.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == model.TerminationRequestId && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(request.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "Clearance items can only be recorded for approved or in-progress requests.");
                return RedirectToAction(nameof(Details), new { id = request.Id });
            }

            model.ReferenceNumber = request.ReferenceNumber;
            model.EmployeeName = request.Employee is null ? null : $"{request.Employee.FirstName} {request.Employee.LastName}".Trim();
            PopulateClearanceOptions(model);

            if (!ModelState.IsValid)
            {
                return View("ClearanceForm", model);
            }

            var item = new ClearanceItem
            {
                TerminationRequestId = request.Id,
                Category = model.Category,
                ResponsiblePerson = model.ResponsiblePerson,
                ResponsibleDepartment = model.ResponsibleDepartment,
                IsMandatory = model.IsMandatory,
                Status = model.Status,
                OutstandingItems = model.OutstandingItems,
                Remarks = model.Remarks,
                CompletedDate = model.Status == ClearanceStatus.Cleared
                    ? model.CompletedDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
                    : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId()
            };

            _dbContext.ClearanceItems.Add(item);
            await TouchRequestAsync(request.Id);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The clearance item was added.");
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditClearance(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var item = await _dbContext.ClearanceItems
                .AsNoTracking()
                .Include(entity => entity.TerminationRequest)
                    .ThenInclude(request => request!.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (item?.TerminationRequest == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(item.TerminationRequest.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "This clearance item can no longer be edited.");
                return RedirectToAction(nameof(Details), new { id = item.TerminationRequestId });
            }

            var model = new ClearanceItemFormViewModel
            {
                Id = item.Id,
                TerminationRequestId = item.TerminationRequestId,
                ReferenceNumber = item.TerminationRequest.ReferenceNumber,
                EmployeeName = item.TerminationRequest.Employee is null
                    ? null
                    : $"{item.TerminationRequest.Employee.FirstName} {item.TerminationRequest.Employee.LastName}".Trim(),
                Category = item.Category,
                ResponsiblePerson = item.ResponsiblePerson,
                ResponsibleDepartment = item.ResponsibleDepartment,
                IsMandatory = item.IsMandatory,
                Status = item.Status,
                OutstandingItems = item.OutstandingItems,
                Remarks = item.Remarks,
                CompletedDate = item.CompletedDate
            };

            PopulateClearanceOptions(model);
            return View("ClearanceForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClearance(int id, ClearanceItemFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            NormalizeClearance(model);

            var item = await _dbContext.ClearanceItems
                .Include(entity => entity.TerminationRequest)
                    .ThenInclude(request => request!.Employee)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (item?.TerminationRequest == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(item.TerminationRequest.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "This clearance item can no longer be edited.");
                return RedirectToAction(nameof(Details), new { id = item.TerminationRequestId });
            }

            model.TerminationRequestId = item.TerminationRequestId;
            model.ReferenceNumber = item.TerminationRequest.ReferenceNumber;
            model.EmployeeName = item.TerminationRequest.Employee is null
                ? null
                : $"{item.TerminationRequest.Employee.FirstName} {item.TerminationRequest.Employee.LastName}".Trim();
            PopulateClearanceOptions(model);

            if (!ModelState.IsValid)
            {
                return View("ClearanceForm", model);
            }

            item.Category = model.Category;
            item.ResponsiblePerson = model.ResponsiblePerson;
            item.ResponsibleDepartment = model.ResponsibleDepartment;
            item.IsMandatory = model.IsMandatory;
            item.Status = model.Status;
            item.OutstandingItems = model.OutstandingItems;
            item.Remarks = model.Remarks;
            item.CompletedDate = model.Status == ClearanceStatus.Cleared
                ? model.CompletedDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
                : null;
            item.UpdatedAt = DateTime.UtcNow;

            await TouchRequestAsync(item.TerminationRequestId);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The clearance item was updated.");
            return RedirectToAction(nameof(Details), new { id = item.TerminationRequestId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateClearanceStatus(int id, ClearanceStatus status, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var item = await _dbContext.ClearanceItems
                .Include(entity => entity.TerminationRequest)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (item?.TerminationRequest == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(item.TerminationRequest.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "This clearance item can no longer be changed.");
                return SafeRedirect(returnUrl, item.TerminationRequestId);
            }

            if (!TerminationWorkflow.CanTransition(item.Status, status))
            {
                TempData.SetToast("warning", "Not Allowed", $"A clearance item cannot move from {item.Status.GetDisplayName()} to {status.GetDisplayName()}.");
                return SafeRedirect(returnUrl, item.TerminationRequestId);
            }

            item.Status = status;
            item.CompletedDate = status == ClearanceStatus.Cleared
                ? item.CompletedDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
                : null;
            item.UpdatedAt = DateTime.UtcNow;

            await TouchRequestAsync(item.TerminationRequestId);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", $"The clearance item was marked as {status.GetDisplayName()}.");
            return SafeRedirect(returnUrl, item.TerminationRequestId);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClearance(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var item = await _dbContext.ClearanceItems
                .Include(entity => entity.TerminationRequest)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (item?.TerminationRequest == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(item.TerminationRequest.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "This clearance item can no longer be removed.");
                return SafeRedirect(returnUrl, item.TerminationRequestId);
            }

            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;

            await TouchRequestAsync(item.TerminationRequestId);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The clearance item was removed.");
            return SafeRedirect(returnUrl, item.TerminationRequestId);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedClearance(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var request = await _dbContext.TerminationRequests
                .Include(entity => entity.Employee)
                .Include(entity => entity.ClearanceItems)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (request == null)
            {
                return NotFound();
            }

            if (!CanManageClearance(request.Status))
            {
                TempData.SetToast("warning", "Not Allowed", "The clearance checklist can only be created for approved or in-progress requests.");
                return SafeRedirect(returnUrl, id);
            }

            if (request.ClearanceItems.Any(item => item.IsActive))
            {
                TempData.SetToast("warning", "Already Created", "This request already has a clearance checklist.");
                return SafeRedirect(returnUrl, id);
            }

            SeedClearanceItems(request);
            await TouchRequestAsync(request.Id);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The standard clearance checklist was created.");
            return SafeRedirect(returnUrl, id);
        }

        // ------------------------------------------------------------------
        // Population helpers
        // ------------------------------------------------------------------

        private async Task PopulateSummaryAsync(TerminationIndexViewModel model)
        {
            var query = _dbContext.TerminationRequests.AsNoTracking().Where(request => request.IsActive);

            model.TotalRequests = await query.CountAsync();
            model.PendingApproval = await query.CountAsync(request => request.Status == TerminationStatus.PendingApproval);
            model.Approved = await query.CountAsync(request => request.Status == TerminationStatus.Approved);
            model.InProgress = await query.CountAsync(request => request.Status == TerminationStatus.InProgress);
            model.Completed = await query.CountAsync(request => request.Status == TerminationStatus.Completed);

            // Requests blocked on clearance: approved or in progress with no
            // checklist yet, or at least one mandatory item still uncleared.
            model.PendingClearance = await query.CountAsync(request =>
                (request.Status == TerminationStatus.Approved || request.Status == TerminationStatus.InProgress)
                && (!request.ClearanceItems.Any(item => item.IsActive)
                    || request.ClearanceItems.Any(item => item.IsActive && item.IsMandatory && item.Status != ClearanceStatus.Cleared)));
        }

        private async Task PopulateEmployeeOptionsAsync(TerminationIndexViewModel model)
        {
            model.EmployeeOptions = await _dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new SelectListItem
                {
                    Value = employee.Id.ToString(),
                    Text = employee.FirstName + " " + employee.LastName + " (" + employee.EmployeeNumber + ")"
                })
                .ToListAsync();
        }

        private async Task PopulateRequestsAsync(TerminationIndexViewModel model, int page)
        {
            var query = _dbContext.TerminationRequests
                .AsNoTracking()
                .Where(request => request.IsActive);

            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                var term = model.Search;
                query = query.Where(request =>
                    request.ReferenceNumber.Contains(term)
                    || request.Reason.Contains(term)
                    || request.Explanation.Contains(term)
                    || (request.Employee != null
                        && (request.Employee.FirstName.Contains(term)
                            || request.Employee.LastName.Contains(term)
                            || request.Employee.EmployeeNumber.Contains(term))));
            }

            if (model.EmployeeId.HasValue)
            {
                query = query.Where(request => request.EmployeeId == model.EmployeeId.Value);
            }

            if (model.Status.HasValue)
            {
                query = query.Where(request => request.Status == model.Status.Value);
            }

            if (model.TerminationType.HasValue)
            {
                query = query.Where(request => request.TerminationType == model.TerminationType.Value);
            }

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = model.TotalRecords == 0
                ? 1
                : (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize);
            model.CurrentPage = Math.Clamp(page, 1, Math.Max(1, model.TotalPages));

            query = model.SortBy switch
            {
                "oldest" => query.OrderBy(request => request.CreatedAt),
                "employee" => query.OrderBy(request => request.Employee!.FirstName).ThenBy(request => request.Employee!.LastName),
                "request" => query.OrderByDescending(request => request.RequestDate),
                "lastday" => query.OrderBy(request => request.ProposedLastWorkingDay),
                "status" => query.OrderBy(request => request.Status).ThenByDescending(request => request.CreatedAt),
                _ => query.OrderByDescending(request => request.CreatedAt)
            };

            model.Requests = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(request => new TerminationListItemViewModel
                {
                    Id = request.Id,
                    ReferenceNumber = request.ReferenceNumber,
                    EmployeeId = request.EmployeeId,
                    EmployeeName = request.Employee == null
                        ? string.Empty
                        : request.Employee.FirstName + " " + request.Employee.LastName,
                    EmployeeNumber = request.Employee == null ? string.Empty : request.Employee.EmployeeNumber,
                    Department = request.Employee == null ? string.Empty : request.Employee.Department,
                    Designation = request.Employee == null ? string.Empty : request.Employee.Designation,
                    TerminationType = request.TerminationType,
                    RequestDate = request.RequestDate,
                    ProposedLastWorkingDay = request.ProposedLastWorkingDay,
                    Status = request.Status,
                    ClearedItems = request.ClearanceItems.Count(item => item.IsActive && item.Status == ClearanceStatus.Cleared),
                    TotalItems = request.ClearanceItems.Count(item => item.IsActive),
                    HasLetter = request.Documents.Any(document => document.IsLetter)
                })
                .ToListAsync();

            foreach (var item in model.Requests)
            {
                item.Initials = Initials(item.EmployeeName);
            }
        }

        private async Task PopulateFormOptionsAsync(TerminationFormViewModel model)
        {
            model.EmployeeOptions = await _dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new SelectListItem
                {
                    Value = employee.Id.ToString(),
                    Text = employee.FirstName + " " + employee.LastName + " (" + employee.EmployeeNumber + ")"
                })
                .ToListAsync();

            model.NoticePeriodOptions = DefaultNoticePeriods
                .Select(period => new SelectListItem { Value = period, Text = period })
                .ToList();
        }

        private static void PopulateClearanceOptions(ClearanceItemFormViewModel model)
        {
            model.DepartmentOptions = HRLists.Departments
                .Select(department => new SelectListItem { Value = department, Text = department })
                .ToList();
        }

        private async Task FillEmployeeContextAsync(TerminationFormViewModel model)
        {
            if (model.EmployeeId <= 0)
            {
                return;
            }

            var employee = await _dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == model.EmployeeId && entity.IsActive);

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "Select a valid active employee.");
                model.EmployeeId = 0;
                return;
            }

            model.EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim();
            model.EmployeeNumber = employee.EmployeeNumber;
            model.EmployeeDepartment = employee.Department;
            model.EmployeeDesignation = employee.Designation;
        }

        private async Task ValidateRequestAsync(TerminationFormViewModel model, int? excludeId)
        {
            if (model.EmployeeId <= 0)
            {
                return;
            }

            var duplicate = await _dbContext.TerminationRequests
                .AsNoTracking()
                .AnyAsync(request => request.IsActive
                    && request.EmployeeId == model.EmployeeId
                    && request.Status != TerminationStatus.Rejected
                    && request.Status != TerminationStatus.Completed
                    && (excludeId == null || request.Id != excludeId.Value));

            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.EmployeeId),
                    "This employee already has an active termination request. Complete or reject it before creating another.");
            }
        }

        // ------------------------------------------------------------------
        // Domain helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Adds the standard five clearance items to a request. Any existing
        /// active items are left untouched, so this is safe to call defensively.
        /// </summary>
        private void SeedClearanceItems(TerminationRequest request)
        {
            var employeeDepartment = request.Employee?.Department;
            if (string.IsNullOrWhiteSpace(employeeDepartment) && request.EmployeeId > 0)
            {
                employeeDepartment = _dbContext.Employees
                    .AsNoTracking()
                    .Where(employee => employee.Id == request.EmployeeId)
                    .Select(employee => employee.Department)
                    .FirstOrDefault();
            }

            var existing = request.ClearanceItems
                .Where(item => item.IsActive)
                .Select(item => item.Category)
                .ToHashSet();

            foreach (var category in DefaultClearanceCategories)
            {
                if (existing.Contains(category))
                {
                    continue;
                }

                request.ClearanceItems.Add(new ClearanceItem
                {
                    Category = category,
                    ResponsibleDepartment = CategoryDepartment(category, employeeDepartment),
                    IsMandatory = true,
                    Status = ClearanceStatus.Pending,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = CurrentUserId()
                });
            }
        }

        /// <summary>
        /// Reflects a completed termination on the employee record without
        /// deleting any history: the employment status is set to Inactive.
        /// </summary>
        private async Task MarkEmployeeTerminatedAsync(int employeeId)
        {
            var employee = await _dbContext.Employees.FirstOrDefaultAsync(entity => entity.Id == employeeId);
            if (employee == null)
            {
                return;
            }

            employee.EmploymentStatus = EmploymentStatus.Inactive;
            employee.UpdatedAt = DateTime.UtcNow;
        }

        private async Task TouchRequestAsync(int requestId)
        {
            var request = await _dbContext.TerminationRequests.FirstOrDefaultAsync(entity => entity.Id == requestId);
            if (request != null)
            {
                request.UpdatedAt = DateTime.UtcNow;
            }
        }

        private async Task<TerminationDocument?> LoadLetterAsync(int requestId)
        {
            if (!CanViewDocuments()) return null;

            return await _dbContext.TerminationDocuments
                .AsNoTracking()
                .Where(document => document.TerminationRequestId == requestId && document.IsLetter)
                .OrderByDescending(document => document.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<TerminationRequest?> LoadDetailsAsync(int id) =>
            await _dbContext.TerminationRequests
                .AsNoTracking()
                .Include(request => request.Employee)
                .Include(request => request.DecidedByUser)
                .Include(request => request.CreatedByUser)
                .Include(request => request.Documents)
                .Include(request => request.ClearanceItems)
                .Include(request => request.StatusHistory)
                    .ThenInclude(history => history.ChangedByUser)
                .FirstOrDefaultAsync(request => request.Id == id && request.IsActive);

        private async Task<string?> ReportingManagerNameAsync(int? managerId)
        {
            if (managerId is null or 0)
            {
                return null;
            }

            return await _dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.Id == managerId.Value)
                .Select(employee => employee.FirstName + " " + employee.LastName)
                .FirstOrDefaultAsync();
        }

        private static string? CategoryDepartment(ClearanceCategory category, string? employeeDepartment) => category switch
        {
            ClearanceCategory.ITAssets => "Information Technology",
            ClearanceCategory.DepartmentHandover => string.IsNullOrWhiteSpace(employeeDepartment) ? "Operations" : employeeDepartment,
            ClearanceCategory.Finance => "Finance",
            ClearanceCategory.Administration => "Administration",
            ClearanceCategory.HR => "Human Resources",
            _ => null
        };

        private static bool CanManageClearance(TerminationStatus status) =>
            status is TerminationStatus.Approved or TerminationStatus.InProgress;

        // ------------------------------------------------------------------
        // Form helpers
        // ------------------------------------------------------------------

        private static void NormalizeForm(TerminationFormViewModel model)
        {
            model.Reason = TrimRequired(model.Reason);
            model.Explanation = TrimRequired(model.Explanation);
            model.NoticePeriod = TrimOptional(model.NoticePeriod);
            model.Remarks = TrimOptional(model.Remarks);
        }

        private static void NormalizeClearance(ClearanceItemFormViewModel model)
        {
            model.ResponsiblePerson = TrimOptional(model.ResponsiblePerson);
            model.ResponsibleDepartment = TrimOptional(model.ResponsibleDepartment);
            model.OutstandingItems = TrimOptional(model.OutstandingItems);
            model.Remarks = TrimOptional(model.Remarks);
        }

        private IActionResult SafeRedirect(string? returnUrl, int requestId) =>
            string.IsNullOrWhiteSpace(returnUrl)
                ? RedirectToAction(nameof(Details), new { id = requestId })
                : Redirect(returnUrl);

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        private bool CanViewDocuments() => CanManage();

        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

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

        private static string Truncate(string? value, int length)
        {
            var copy = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(copy))
            {
                return "document.pdf";
            }

            return copy.Length <= length ? copy : copy[..length].TrimEnd();
        }

        private static string TrimRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
