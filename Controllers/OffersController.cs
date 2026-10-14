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
    /// Screen 3 — Offer &amp; Hiring. A single hub for the offer workflow:
    /// summary cards, an offers listing, and a hiring listing. Offers are
    /// created against an eligible job application and carry a generated
    /// reference, a secure offer letter, and supporting documents.
    ///
    /// Accepted offers can be converted into an employee (via the existing
    /// Employee Management screens), closing the recruitment loop.
    /// </summary>
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class OffersController : Controller
    {
        private const int DefaultPageSize = 10;
        private static readonly int[] AllowedPageSizes = { 10, 25, 50 };

        private const string DocumentsFolder = "offers";
        private const int MaxSupportingDocuments = 5;

        private readonly ApplicationDbContext _dbContext;
        private readonly IOfferNumberService _offerNumberService;
        private readonly IJobApplicationFileService _fileService;
        private readonly ISimplePdfGenerator _pdfGenerator;

        public OffersController(
            ApplicationDbContext dbContext,
            IOfferNumberService offerNumberService,
            IJobApplicationFileService fileService,
            ISimplePdfGenerator pdfGenerator)
        {
            _dbContext = dbContext;
            _offerNumberService = offerNumberService;
            _fileService = fileService;
            _pdfGenerator = pdfGenerator;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? tab = null,
            string? search = null,
            string? department = null,
            JobOfferStatus? offerStatus = null,
            string? offerSort = null,
            string? hiringSort = null,
            int? page = null,
            int? pageSize = null)
        {
            await ApplyExpiryAsync();

            var activeTab = string.Equals(tab, "hiring", StringComparison.OrdinalIgnoreCase) ? "hiring" : "offers";
            var resolvedPageSize = pageSize.HasValue && AllowedPageSizes.Contains(pageSize.Value)
                ? pageSize.Value
                : DefaultPageSize;

            var model = new OfferIndexViewModel
            {
                ActiveTab = activeTab,
                CanManage = CanManage(),
                PageSize = resolvedPageSize,
                OfferSearch = activeTab == "offers" ? search : null,
                OfferDepartment = department,
                OfferStatus = offerStatus,
                OfferSortBy = string.IsNullOrWhiteSpace(offerSort) ? "newest" : offerSort,
                HiringSearch = activeTab == "hiring" ? search : null,
                HiringSortBy = string.IsNullOrWhiteSpace(hiringSort) ? "newest" : hiringSort
            };

            await PopulateSummaryAsync(model);

            if (activeTab == "hiring")
            {
                await PopulateHiringAsync(model, page ?? 1);
            }
            else
            {
                await PopulateOffersAsync(model, page ?? 1);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? applicationId = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var model = new OfferFormViewModel
            {
                ReferenceNumber = await _offerNumberService.GenerateNextReferenceAsync(),
                OfferDate = today,
                ExpiryDate = today.AddDays(7),
                JoiningDate = today.AddDays(30),
                Currency = HRLists.Currencies[0],
                EmploymentType = EmploymentType.FullTime
            };

            if (applicationId.HasValue && await IsEligibleApplicationAsync(applicationId.Value, null))
            {
                model.JobApplicationId = applicationId.Value;
                await FillContextAsync(model);
                await PrefillFromApplicationAsync(model);
            }

            await PopulateOfferOptionsAsync(model);
            return View("Form", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OfferFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            NormalizeForm(model);
            await PopulateOfferOptionsAsync(model);
            await FillContextAsync(model);
            await ValidateOfferAsync(model);

            if (!ModelState.IsValid)
            {
                model.ReferenceNumber ??= await _offerNumberService.GenerateNextReferenceAsync();
                return View("Form", model);
            }

            var offer = new JobOffer
            {
                ReferenceNumber = await _offerNumberService.GenerateNextReferenceAsync(),
                JobApplicationId = model.JobApplicationId,
                Designation = model.Designation,
                Department = model.Department,
                EmploymentType = model.EmploymentType,
                WorkLocation = model.WorkLocation,
                OfferedSalary = model.OfferedSalary!.Value,
                Currency = model.Currency,
                SalaryFrequency = model.SalaryFrequency,
                OfferDate = model.OfferDate!.Value,
                ExpiryDate = model.ExpiryDate!.Value,
                JoiningDate = model.JoiningDate!.Value,
                ProbationPeriodMonths = model.ProbationPeriodMonths,
                Benefits = TrimOptional(model.Benefits),
                Terms = TrimOptional(model.Terms),
                ReportingManagerId = model.ReportingManagerId,
                Status = JobOfferStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            _dbContext.JobOffers.Add(offer);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Guards the race against the filtered unique index (one active
                // offer per application).
                ModelState.AddModelError(nameof(model.JobApplicationId),
                    "An active offer already exists for this candidate. Finish or withdraw the existing offer before creating a new one.");
                return View("Form", model);
            }

            TempData.SetToast("success", "Success", $"Offer {offer.ReferenceNumber} created as a draft.");
            return RedirectToAction(nameof(Details), new { id = offer.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await _dbContext.JobOffers
                .AsNoTracking()
                .Include(entity => entity.JobApplication)
                .ThenInclude(application => application!.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (offer == null)
            {
                return NotFound();
            }

            if (!IsEditable(offer))
            {
                TempData.SetToast("warning", "Not Allowed", "This offer is no longer editable.");
                return RedirectToAction(nameof(Details), new { id });
            }

            var model = new OfferFormViewModel
            {
                Id = offer.Id,
                ReferenceNumber = offer.ReferenceNumber,
                JobApplicationId = offer.JobApplicationId,
                ApplicationNumber = offer.JobApplication?.ApplicationNumber,
                CandidateName = offer.JobApplication is null ? null : $"{offer.JobApplication.FirstName} {offer.JobApplication.LastName}".Trim(),
                CandidateEmail = offer.JobApplication?.Email,
                SourceJobTitle = offer.JobApplication?.JobPosting?.JobTitle,
                SourceJobReference = offer.JobApplication?.JobPosting?.ReferenceNumber,
                Designation = offer.Designation,
                Department = offer.Department,
                EmploymentType = offer.EmploymentType,
                WorkLocation = offer.WorkLocation,
                OfferedSalary = offer.OfferedSalary,
                Currency = offer.Currency,
                SalaryFrequency = offer.SalaryFrequency,
                OfferDate = offer.OfferDate,
                ExpiryDate = offer.ExpiryDate,
                JoiningDate = offer.JoiningDate,
                ProbationPeriodMonths = offer.ProbationPeriodMonths,
                Benefits = offer.Benefits,
                Terms = offer.Terms,
                ReportingManagerId = offer.ReportingManagerId,
                Status = offer.Status
            };

            await PopulateOfferOptionsAsync(model);
            return View("Form", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OfferFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;
            if (model.Id != id) return NotFound();

            NormalizeForm(model);
            await PopulateOfferOptionsAsync(model);
            await FillContextAsync(model);
            await ValidateOfferAsync(model);

            var offer = await _dbContext.JobOffers.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (offer == null)
            {
                return NotFound();
            }

            if (!IsEditable(offer))
            {
                TempData.SetToast("warning", "Not Allowed", "This offer is no longer editable.");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!ModelState.IsValid)
            {
                return View("Form", model);
            }

            offer.JobApplicationId = model.JobApplicationId;
            offer.Designation = model.Designation;
            offer.Department = model.Department;
            offer.EmploymentType = model.EmploymentType;
            offer.WorkLocation = model.WorkLocation;
            offer.OfferedSalary = model.OfferedSalary!.Value;
            offer.Currency = model.Currency;
            offer.SalaryFrequency = model.SalaryFrequency;
            offer.OfferDate = model.OfferDate!.Value;
            offer.ExpiryDate = model.ExpiryDate!.Value;
            offer.JoiningDate = model.JoiningDate!.Value;
            offer.ProbationPeriodMonths = model.ProbationPeriodMonths;
            offer.Benefits = TrimOptional(model.Benefits);
            offer.Terms = TrimOptional(model.Terms);
            offer.ReportingManagerId = model.ReportingManagerId;
            offer.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(model.JobApplicationId),
                    "An active offer already exists for this candidate.");
                return View("Form", model);
            }

            TempData.SetToast("success", "Success", $"Offer {offer.ReferenceNumber} updated.");
            return RedirectToAction(nameof(Details), new { id = offer.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            await ApplyExpiryAsync();

            var offer = await _dbContext.JobOffers
                .AsNoTracking()
                .Include(entity => entity.JobApplication)
                .ThenInclude(application => application!.JobPosting)
                .Include(entity => entity.Documents)
                .Include(entity => entity.ReportingManager)
                .Include(entity => entity.Employee)
                .Include(entity => entity.CreatedByUser)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (offer == null || offer.JobApplication == null)
            {
                return NotFound();
            }

            var application = offer.JobApplication;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var model = new OfferDetailsViewModel
            {
                Offer = offer,
                Application = application,
                Posting = application.JobPosting,
                CandidateName = $"{application.FirstName} {application.LastName}".Trim(),
                Initials = Initials(application.FirstName, application.LastName),
                ReportingManagerName = offer.ReportingManager is null
                    ? null
                    : $"{offer.ReportingManager.FirstName} {offer.ReportingManager.LastName}".Trim(),
                Letter = offer.Documents.FirstOrDefault(document => document.IsLetter),
                CanManage = CanManage(),
                CanHire = CanManage()
                    && offer.Status == JobOfferStatus.Accepted
                    && offer.EmployeeId == null
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await LoadForActionAsync(id);
            if (offer == null) return NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (offer.Status == JobOfferStatus.Draft && offer.ExpiryDate < today)
            {
                offer.Status = JobOfferStatus.Expired;
                offer.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                TempData.SetToast("warning", "Offer Expired", "This offer's expiry date has passed and it was marked as expired.");
            }
            else if (offer.Status != JobOfferStatus.Draft)
            {
                TempData.SetToast("warning", "Not Allowed", "Only a draft offer can be issued.");
            }
            else
            {
                offer.Status = JobOfferStatus.PendingAcceptance;
                offer.IssuedAt = DateTime.UtcNow;
                offer.UpdatedAt = DateTime.UtcNow;

                // Keep the application pipeline in step with the offer.
                var application = await _dbContext.JobApplications.FirstOrDefaultAsync(entity => entity.Id == offer.JobApplicationId);
                if (application != null && application.Status != JobApplicationStatus.Hired && application.Status != JobApplicationStatus.Rejected)
                {
                    application.Status = JobApplicationStatus.Offer;
                    application.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                TempData.SetToast("success", "Success", $"Offer {offer.ReferenceNumber} was issued and is pending acceptance.");
            }

            return SafeRedirect(returnUrl, offer.Id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await LoadForActionAsync(id);
            if (offer == null) return NotFound();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (offer.Status is not (JobOfferStatus.Draft or JobOfferStatus.PendingAcceptance))
            {
                TempData.SetToast("warning", "Not Allowed", "This offer cannot be marked as accepted.");
            }
            else if (offer.ExpiryDate < today)
            {
                offer.Status = JobOfferStatus.Expired;
                offer.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                TempData.SetToast("warning", "Offer Expired", "An expired offer cannot be accepted.");
            }
            else
            {
                offer.Status = JobOfferStatus.Accepted;
                offer.RespondedAt = DateTime.UtcNow;
                offer.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                TempData.SetToast("success", "Success", $"{offer.ReferenceNumber} was marked as accepted. You can now create the employee record.");
            }

            return SafeRedirect(returnUrl, offer.Id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await LoadForActionAsync(id);
            if (offer == null) return NotFound();

            if (offer.Status != JobOfferStatus.PendingAcceptance)
            {
                TempData.SetToast("warning", "Not Allowed", "Only an issued offer can be rejected.");
            }
            else
            {
                offer.Status = JobOfferStatus.Rejected;
                offer.RespondedAt = DateTime.UtcNow;
                offer.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                TempData.SetToast("success", "Success", $"Offer {offer.ReferenceNumber} was marked as rejected.");
            }

            return SafeRedirect(returnUrl, offer.Id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await LoadForActionAsync(id);
            if (offer == null) return NotFound();

            if (offer.Status is not (JobOfferStatus.Draft or JobOfferStatus.PendingAcceptance))
            {
                TempData.SetToast("warning", "Not Allowed", "Only a draft or pending offer can be withdrawn.");
            }
            else
            {
                offer.Status = JobOfferStatus.Withdrawn;
                offer.WithdrawnAt = DateTime.UtcNow;
                offer.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                TempData.SetToast("success", "Success", $"Offer {offer.ReferenceNumber} was withdrawn.");
            }

            return SafeRedirect(returnUrl, offer.Id);
        }

        /// <summary>
        /// Hiring action. Sends the authorized user to the existing Employee
        /// creation screen, prefilled from the accepted offer. The employee is
        /// only created once the user confirms, and the offer is then linked.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RecordJoining(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await _dbContext.JobOffers
                .AsNoTracking()
                .Include(entity => entity.JobApplication)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (offer == null)
            {
                return NotFound();
            }

            if (offer.EmployeeId.HasValue)
            {
                TempData.SetToast("warning", "Already Hired", "An employee record already exists for this offer.");
                return RedirectToAction("Details", "Employees", new { id = offer.EmployeeId.Value });
            }

            if (offer.Status != JobOfferStatus.Accepted)
            {
                TempData.SetToast("warning", "Not Allowed", "The offer must be accepted before the candidate can join.");
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction("Create", "Employees", new { offerId = offer.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateLetter(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await _dbContext.JobOffers
                .Include(entity => entity.JobApplication)
                .ThenInclude(application => application!.JobPosting)
                .Include(entity => entity.Documents)
                .Include(entity => entity.ReportingManager)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (offer == null || offer.JobApplication == null)
            {
                return NotFound();
            }

            var settings = await _dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            var companyName = string.IsNullOrWhiteSpace(settings?.ArenaName) ? "GameHub Arena" : settings!.ArenaName;

            var lines = OfferLetterComposer.Compose(
                offer,
                offer.JobApplication,
                offer.JobApplication.JobPosting,
                offer.ReportingManager is null
                    ? null
                    : $"{offer.ReportingManager.FirstName} {offer.ReportingManager.LastName}".Trim(),
                companyName,
                settings?.Address,
                settings?.Phone ?? string.Empty,
                settings?.Email ?? string.Empty);

            var bytes = _pdfGenerator.Generate(OfferLetterComposer.LetterTitle, lines);

            // Replace any previously generated letter. Supporting documents are
            // untouched.
            foreach (var existing in offer.Documents.Where(document => document.IsLetter).ToList())
            {
                await _fileService.DeleteAsync(existing.StoredFileName, DocumentsFolder);
                _dbContext.JobOfferDocuments.Remove(existing);
            }

            var storedName = await _fileService.SaveBytesAsync(bytes, DocumentsFolder);
            _dbContext.JobOfferDocuments.Add(new JobOfferDocument
            {
                JobOfferId = offer.Id,
                StoredFileName = storedName,
                OriginalFileName = $"{offer.ReferenceNumber}-Offer-Letter.pdf",
                ContentType = "application/pdf",
                FileSize = bytes.Length,
                IsLetter = true,
                IsGenerated = true,
                Title = "Offer Letter",
                CreatedAt = DateTime.UtcNow
            });

            offer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The offer letter was generated.");
            return SafeRedirect(returnUrl, offer.Id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocuments(int id, List<IFormFile>? documents, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await _dbContext.JobOffers.FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (offer == null)
            {
                return NotFound();
            }

            var files = (documents ?? new List<IFormFile>()).Where(file => file.Length > 0).ToList();
            if (files.Count == 0)
            {
                TempData.SetToast("warning", "No Documents", "Select at least one PDF to upload.");
                return SafeRedirect(returnUrl, offer.Id);
            }

            var existingCount = await _dbContext.JobOfferDocuments
                .CountAsync(document => document.JobOfferId == id && !document.IsLetter);
            if (existingCount + files.Count > MaxSupportingDocuments)
            {
                TempData.SetToast("warning", "Limit Reached",
                    $"A maximum of {MaxSupportingDocuments} supporting documents is allowed per offer.");
                return SafeRedirect(returnUrl, offer.Id);
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

                    TempData.SetToast("error", "Upload Failed", result?.ErrorMessage ?? "A document could not be uploaded.");
                    return SafeRedirect(returnUrl, offer.Id);
                }

                saved.Add((result.StoredFileName!, file));
            }

            foreach (var (stored, file) in saved)
            {
                _dbContext.JobOfferDocuments.Add(new JobOfferDocument
                {
                    JobOfferId = offer.Id,
                    StoredFileName = stored,
                    OriginalFileName = Truncate(file.FileName, 255),
                    ContentType = "application/pdf",
                    FileSize = file.Length,
                    IsLetter = false,
                    IsGenerated = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            offer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", $"{(saved.Count == 1 ? "1 document was" : $"{saved.Count} documents were")} uploaded.");
            return SafeRedirect(returnUrl, offer.Id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var document = await _dbContext.JobOfferDocuments.FirstOrDefaultAsync(entity => entity.Id == id);
            if (document == null)
            {
                return NotFound();
            }

            var offerId = document.JobOfferId;
            await _fileService.DeleteAsync(document.StoredFileName, DocumentsFolder);
            _dbContext.JobOfferDocuments.Remove(document);
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The document was removed.");
            return SafeRedirect(returnUrl, offerId);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            if (!CanViewDocuments()) return NotFound();

            var document = await _dbContext.JobOfferDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == id);
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

        /// <summary>Streams the offer letter as an attachment (authorized only).</summary>
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

        /// <summary>Streams the offer letter inline for in-browser preview.</summary>
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

        /// <summary>
        /// Replaces the offer letter with an uploaded PDF. The letter is
        /// optional and only available for offers that are not yet closed.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadLetter(int id, IFormFile? letter, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var offer = await _dbContext.JobOffers
                .Include(entity => entity.Documents)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (offer == null)
            {
                return NotFound();
            }

            if (letter == null || letter.Length == 0)
            {
                TempData.SetToast("warning", "No File", "Select a PDF offer letter to upload.");
                return SafeRedirect(returnUrl, offer.Id);
            }

            var result = await _fileService.SavePdfAsync(letter, DocumentsFolder);
            if (result is not { Success: true })
            {
                TempData.SetToast("error", "Upload Failed", result?.ErrorMessage ?? "The offer letter could not be uploaded.");
                return SafeRedirect(returnUrl, offer.Id);
            }

            foreach (var existing in offer.Documents.Where(document => document.IsLetter).ToList())
            {
                await _fileService.DeleteAsync(existing.StoredFileName, DocumentsFolder);
                _dbContext.JobOfferDocuments.Remove(existing);
            }

            _dbContext.JobOfferDocuments.Add(new JobOfferDocument
            {
                JobOfferId = offer.Id,
                StoredFileName = result.StoredFileName!,
                OriginalFileName = Truncate(letter.FileName, 255),
                ContentType = "application/pdf",
                FileSize = letter.Length,
                IsLetter = true,
                IsGenerated = false,
                Title = "Offer Letter",
                CreatedAt = DateTime.UtcNow
            });

            offer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData.SetToast("success", "Success", "The offer letter was uploaded.");
            return SafeRedirect(returnUrl, offer.Id);
        }

        private async Task<JobOfferDocument?> LoadLetterAsync(int offerId)
        {
            if (!CanViewDocuments()) return null;

            return await _dbContext.JobOfferDocuments
                .AsNoTracking()
                .Where(document => document.JobOfferId == offerId && document.IsLetter)
                .OrderByDescending(document => document.Id)
                .FirstOrDefaultAsync();
        }

        // ------------------------------------------------------------------
        // Data loading
        // ------------------------------------------------------------------

        private async Task PopulateSummaryAsync(OfferIndexViewModel model)
        {
            model.TotalOffers = await _dbContext.JobOffers.AsNoTracking().CountAsync(offer => offer.IsActive);
            model.DraftOffers = await _dbContext.JobOffers.AsNoTracking()
                .CountAsync(offer => offer.IsActive && offer.Status == JobOfferStatus.Draft);
            model.PendingAcceptance = await _dbContext.JobOffers.AsNoTracking()
                .CountAsync(offer => offer.IsActive && offer.Status == JobOfferStatus.PendingAcceptance);
            model.AcceptedOffers = await _dbContext.JobOffers.AsNoTracking()
                .CountAsync(offer => offer.IsActive && offer.Status == JobOfferStatus.Accepted);
            model.RejectedOrWithdrawn = await _dbContext.JobOffers.AsNoTracking()
                .CountAsync(offer => offer.IsActive
                    && (offer.Status == JobOfferStatus.Rejected || offer.Status == JobOfferStatus.Withdrawn));

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            model.PendingJoining = await _dbContext.JobOffers.AsNoTracking()
                .CountAsync(offer => offer.IsActive
                    && offer.Status == JobOfferStatus.Accepted
                    && offer.JoiningDate > today);
        }

        private async Task PopulateOffersAsync(OfferIndexViewModel model, int page)
        {
            var query = _dbContext.JobOffers.AsNoTracking().Where(offer => offer.IsActive);

            if (!string.IsNullOrWhiteSpace(model.OfferSearch))
            {
                var term = model.OfferSearch.Trim();
                query = query.Where(offer =>
                    offer.ReferenceNumber.Contains(term)
                    || offer.Designation.Contains(term)
                    || offer.JobApplication!.ApplicationNumber.Contains(term)
                    || offer.JobApplication!.FirstName.Contains(term)
                    || offer.JobApplication!.LastName.Contains(term)
                    || (offer.JobApplication!.FirstName + " " + offer.JobApplication!.LastName).Contains(term)
                    || offer.JobApplication!.Email.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(model.OfferDepartment))
            {
                query = query.Where(offer => offer.Department == model.OfferDepartment);
            }

            if (model.OfferStatus.HasValue)
            {
                query = query.Where(offer => offer.Status == model.OfferStatus.Value);
            }

            query = model.OfferSortBy switch
            {
                "oldest" => query.OrderBy(offer => offer.CreatedAt),
                "candidate" => query.OrderBy(offer => offer.JobApplication!.FirstName).ThenBy(offer => offer.JobApplication!.LastName),
                "joining" => query.OrderBy(offer => offer.JoiningDate),
                "expiry" => query.OrderBy(offer => offer.ExpiryDate),
                "salary" => query.OrderByDescending(offer => offer.OfferedSalary),
                _ => query.OrderByDescending(offer => offer.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Offers = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(offer => new OfferListItemViewModel
                {
                    Id = offer.Id,
                    ReferenceNumber = offer.ReferenceNumber,
                    JobApplicationId = offer.JobApplicationId,
                    ApplicationNumber = offer.JobApplication!.ApplicationNumber,
                    CandidateName = (offer.JobApplication!.FirstName + " " + offer.JobApplication!.LastName).Trim(),
                    CandidateEmail = offer.JobApplication!.Email,
                    JobTitle = offer.JobApplication!.JobPosting != null ? offer.JobApplication!.JobPosting!.JobTitle : string.Empty,
                    Designation = offer.Designation,
                    Department = offer.Department,
                    EmploymentType = offer.EmploymentType,
                    OfferedSalary = offer.OfferedSalary,
                    Currency = offer.Currency,
                    SalaryFrequency = offer.SalaryFrequency,
                    OfferDate = offer.OfferDate,
                    ExpiryDate = offer.ExpiryDate,
                    JoiningDate = offer.JoiningDate,
                    Status = offer.Status,
                    HasLetter = offer.Documents.Any(document => document.IsLetter),
                    DocumentCount = offer.Documents.Count(document => !document.IsLetter),
                    EmployeeId = offer.EmployeeId,
                    EmployeeNumber = offer.Employee != null ? offer.Employee.EmployeeNumber : null
                })
                .ToListAsync();

            foreach (var item in model.Offers)
            {
                item.Initials = Initials(item.CandidateName);
            }

            model.Departments = await _dbContext.JobOffers.AsNoTracking()
                .Where(offer => offer.IsActive)
                .Select(offer => offer.Department)
                .Distinct()
                .OrderBy(department => department)
                .ToListAsync();
        }

        private async Task PopulateHiringAsync(OfferIndexViewModel model, int page)
        {
            var query = _dbContext.JobOffers.AsNoTracking()
                .Where(offer => offer.IsActive
                    && (offer.Status == JobOfferStatus.Accepted || offer.Status == JobOfferStatus.Hired));

            if (!string.IsNullOrWhiteSpace(model.HiringSearch))
            {
                var term = model.HiringSearch.Trim();
                query = query.Where(offer =>
                    offer.ReferenceNumber.Contains(term)
                    || offer.Designation.Contains(term)
                    || offer.JobApplication!.ApplicationNumber.Contains(term)
                    || offer.JobApplication!.FirstName.Contains(term)
                    || offer.JobApplication!.LastName.Contains(term)
                    || (offer.JobApplication!.FirstName + " " + offer.JobApplication!.LastName).Contains(term));
            }

            query = model.HiringSortBy switch
            {
                "oldest" => query.OrderBy(offer => offer.CreatedAt),
                "candidate" => query.OrderBy(offer => offer.JobApplication!.FirstName).ThenBy(offer => offer.JobApplication!.LastName),
                "joining" => query.OrderBy(offer => offer.JoiningDate),
                _ => query.OrderByDescending(offer => offer.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Hiring = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(offer => new OfferListItemViewModel
                {
                    Id = offer.Id,
                    ReferenceNumber = offer.ReferenceNumber,
                    JobApplicationId = offer.JobApplicationId,
                    ApplicationNumber = offer.JobApplication!.ApplicationNumber,
                    CandidateName = (offer.JobApplication!.FirstName + " " + offer.JobApplication!.LastName).Trim(),
                    CandidateEmail = offer.JobApplication!.Email,
                    JobTitle = offer.JobApplication!.JobPosting != null ? offer.JobApplication!.JobPosting!.JobTitle : string.Empty,
                    Designation = offer.Designation,
                    Department = offer.Department,
                    EmploymentType = offer.EmploymentType,
                    OfferedSalary = offer.OfferedSalary,
                    Currency = offer.Currency,
                    SalaryFrequency = offer.SalaryFrequency,
                    OfferDate = offer.OfferDate,
                    ExpiryDate = offer.ExpiryDate,
                    JoiningDate = offer.JoiningDate,
                    Status = offer.Status,
                    HasLetter = offer.Documents.Any(document => document.IsLetter),
                    DocumentCount = offer.Documents.Count(document => !document.IsLetter),
                    EmployeeId = offer.EmployeeId,
                    EmployeeNumber = offer.Employee != null ? offer.Employee.EmployeeNumber : null
                })
                .ToListAsync();

            foreach (var item in model.Hiring)
            {
                item.Initials = Initials(item.CandidateName);
            }
        }

        private async Task PopulateOfferOptionsAsync(OfferFormViewModel model)
        {
            var excludeOfferId = model.Id;

            var eligible = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(application => application.IsActive
                    && application.Status != JobApplicationStatus.Rejected
                    && application.Status != JobApplicationStatus.Hired
                    && (application.Status == JobApplicationStatus.Shortlisted
                        || application.Status == JobApplicationStatus.Interview
                        || application.Status == JobApplicationStatus.Offer)
                    && !application.Offers.Any(offer => offer.IsActive
                        && (excludeOfferId == null || offer.Id != excludeOfferId)
                        && (offer.Status == JobOfferStatus.Draft
                            || offer.Status == JobOfferStatus.PendingAcceptance
                            || offer.Status == JobOfferStatus.Accepted
                            || offer.Status == JobOfferStatus.Hired)))
                .OrderBy(application => application.FirstName)
                .ThenBy(application => application.LastName)
                .Select(application => new
                {
                    application.Id,
                    application.ApplicationNumber,
                    application.FirstName,
                    application.LastName,
                    JobTitle = application.JobPosting != null ? application.JobPosting.JobTitle : string.Empty
                })
                .ToListAsync();

            model.Applications = eligible
                .Select(application => new SelectListItem(
                    $"{application.ApplicationNumber} - {application.FirstName} {application.LastName} ({application.JobTitle})".TrimEnd(),
                    application.Id.ToString(),
                    application.Id == model.JobApplicationId))
                .ToList();

            var managers = await _dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive)
                .OrderBy(employee => employee.FirstName)
                .ThenBy(employee => employee.LastName)
                .Select(employee => new
                {
                    employee.Id,
                    Name = (employee.FirstName + " " + employee.LastName).Trim(),
                    employee.EmployeeNumber
                })
                .ToListAsync();

            model.ReportingManagers = managers
                .Select(manager => new SelectListItem(
                    $"{manager.Name} ({manager.EmployeeNumber})",
                    manager.Id.ToString(),
                    manager.Id == model.ReportingManagerId))
                .ToList();
        }

        private async Task FillContextAsync(OfferFormViewModel model)
        {
            if (model.JobApplicationId <= 0)
            {
                return;
            }

            var application = await _dbContext.JobApplications
                .AsNoTracking()
                .Include(entity => entity.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == model.JobApplicationId);

            if (application == null)
            {
                return;
            }

            model.ApplicationNumber = application.ApplicationNumber;
            model.CandidateName = $"{application.FirstName} {application.LastName}".Trim();
            model.CandidateEmail = application.Email;
            model.SourceJobTitle = application.JobPosting?.JobTitle;
            model.SourceJobReference = application.JobPosting?.ReferenceNumber;
        }

        private async Task PrefillFromApplicationAsync(OfferFormViewModel model)
        {
            var application = await _dbContext.JobApplications
                .AsNoTracking()
                .Include(entity => entity.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == model.JobApplicationId);

            var posting = application?.JobPosting;
            if (posting is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(model.Designation)) model.Designation = posting.JobTitle;
            if (string.IsNullOrWhiteSpace(model.Department)) model.Department = posting.Department;
            if (string.IsNullOrWhiteSpace(model.WorkLocation)) model.WorkLocation = posting.JobLocation;
            if (string.IsNullOrWhiteSpace(model.Currency)) model.Currency = posting.Currency;
        }

        private async Task ValidateOfferAsync(OfferFormViewModel model)
        {
            if (model.JobApplicationId <= 0)
            {
                return;
            }

            var eligible = await IsEligibleApplicationAsync(model.JobApplicationId, model.Id);
            if (!eligible)
            {
                ModelState.AddModelError(nameof(model.JobApplicationId),
                    "The selected candidate is not eligible for a new offer (already hired, rejected, or holding an active offer).");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (model.ExpiryDate.HasValue && model.ExpiryDate.Value < today)
            {
                ModelState.AddModelError(nameof(model.ExpiryDate), "Offer Expiry Date cannot be in the past.");
            }
        }

        private async Task<bool> IsEligibleApplicationAsync(int applicationId, int? excludeOfferId)
        {
            return await _dbContext.JobApplications.AsNoTracking().AnyAsync(application =>
                application.Id == applicationId
                && application.IsActive
                && application.Status != JobApplicationStatus.Rejected
                && application.Status != JobApplicationStatus.Hired
                && (application.Status == JobApplicationStatus.Shortlisted
                    || application.Status == JobApplicationStatus.Interview
                    || application.Status == JobApplicationStatus.Offer)
                && !application.Offers.Any(offer => offer.IsActive
                    && (excludeOfferId == null || offer.Id != excludeOfferId)
                    && (offer.Status == JobOfferStatus.Draft
                        || offer.Status == JobOfferStatus.PendingAcceptance
                        || offer.Status == JobOfferStatus.Accepted
                        || offer.Status == JobOfferStatus.Hired)));
        }

        private Task ApplyExpiryAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return ApplyExpiryAsync(today);
        }

        private async Task ApplyExpiryAsync(DateOnly today)
        {
            var expiring = await _dbContext.JobOffers
                .Where(offer => offer.IsActive
                    && offer.Status == JobOfferStatus.PendingAcceptance
                    && offer.ExpiryDate < today)
                .ToListAsync();

            if (expiring.Count == 0)
            {
                return;
            }

            foreach (var offer in expiring)
            {
                offer.Status = JobOfferStatus.Expired;
                offer.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task<JobOffer?> LoadForActionAsync(int id) =>
            await _dbContext.JobOffers.FirstOrDefaultAsync(offer => offer.Id == id && offer.IsActive);

        private static bool IsEditable(JobOffer offer) =>
            offer.Status is JobOfferStatus.Draft or JobOfferStatus.PendingAcceptance;

        private static void NormalizeForm(OfferFormViewModel model)
        {
            model.Designation = TrimRequired(model.Designation);
            model.Department = TrimRequired(model.Department);
            model.WorkLocation = TrimRequired(model.WorkLocation);
            model.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "PKR" : model.Currency.Trim().ToUpperInvariant();
            model.Benefits = TrimOptional(model.Benefits);
            model.Terms = TrimOptional(model.Terms);
        }

        private IActionResult SafeRedirect(string? returnUrl, int offerId)
            => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(nameof(Details), new { id = offerId });

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        /// <summary>
        /// Offer letters and supporting documents are only exposed to HR
        /// management roles.
        /// </summary>
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

        private static string TrimRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Truncate(string value, int maxLength)
        {
            var trimmed = value ?? string.Empty;
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
