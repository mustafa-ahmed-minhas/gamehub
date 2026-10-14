using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [AllowAnonymous]
    public class CareersController : Controller
    {
        private const int DescriptionPreviewLength = 320;

        // Must match the folders used by the HR JobApplicationsController so
        // public applications are readable/downloadable from the HR screens.
        private const string CvFolder = "cvs";
        private const string DocumentsFolder = "documents";

        private readonly ApplicationDbContext _dbContext;
        private readonly IJobApplicationNumberService _applicationNumberService;
        private readonly IJobApplicationFileService _fileService;
        private readonly IJobApplicationDuplicateService _duplicateService;

        public CareersController(
            ApplicationDbContext dbContext,
            IJobApplicationNumberService applicationNumberService,
            IJobApplicationFileService fileService,
            IJobApplicationDuplicateService duplicateService)
        {
            _dbContext = dbContext;
            _applicationNumberService = applicationNumberService;
            _fileService = fileService;
            _duplicateService = duplicateService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new PublicCareersIndexViewModel();

            try
            {
                var positions = await EligiblePostings()
                    .OrderByDescending(posting => posting.CreatedAt)
                    .ThenBy(posting => posting.JobTitle)
                    .Select(posting => new PublicJobOpeningListItemViewModel
                    {
                        Id = posting.Id,
                        JobTitle = posting.JobTitle,
                        Department = posting.Department,
                        JobLocation = posting.JobLocation,
                        EmploymentType = posting.EmploymentType,
                        WorkplaceType = posting.WorkplaceType,
                        Vacancies = posting.Vacancies,
                        PostedOn = DateOnly.FromDateTime(posting.CreatedAt),
                        ApplicationDeadline = posting.ApplicationDeadline,
                        JobDescription = posting.JobDescription
                    })
                    .ToListAsync();

                foreach (var position in positions)
                {
                    position.JobDescription = BuildDescriptionPreview(position.JobDescription);
                }

                model.OpenPositions = positions;
            }
            catch (Exception)
            {
                model.LoadFailed = true;
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var posting = await EligiblePostings()
                    .Where(entity => entity.Id == id)
                    .Select(entity => new PublicJobPostingViewModel
                    {
                        Id = entity.Id,
                        ReferenceNumber = entity.ReferenceNumber,
                        JobTitle = entity.JobTitle,
                        Department = entity.Department,
                        EmploymentType = entity.EmploymentType,
                        WorkplaceType = entity.WorkplaceType,
                        JobLocation = entity.JobLocation,
                        Vacancies = entity.Vacancies,
                        MinimumSalary = entity.MinimumSalary,
                        MaximumSalary = entity.MaximumSalary,
                        Currency = entity.Currency,
                        ExperienceRequired = entity.ExperienceRequired,
                        MinimumEducation = entity.MinimumEducation,
                        JobDescription = entity.JobDescription,
                        KeyResponsibilities = entity.KeyResponsibilities,
                        Qualifications = entity.Qualifications,
                        Benefits = entity.Benefits,
                        Skills = entity.Skills
                            .OrderBy(skill => skill.Id)
                            .Select(skill => skill.Skill)
                            .ToList(),
                        PostedOn = DateOnly.FromDateTime(entity.CreatedAt),
                        ApplicationDeadline = entity.ApplicationDeadline
                    })
                    .FirstOrDefaultAsync();

                if (posting == null)
                {
                    // Draft, closed, expired, removed, or unknown postings are treated
                    // exactly like a missing page.
                    return NotFound();
                }

                ViewData["Title"] = posting.JobTitle;
                return View(new PublicJobDetailsViewModel { Posting = posting });
            }
            catch (Exception)
            {
                ViewData["Title"] = "Job Details";
                return View(new PublicJobDetailsViewModel { LoadFailed = true });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Apply(int id)
        {
            PublicJobPostingViewModel? posting;
            try
            {
                posting = await LoadPostingHeaderAsync(id);
            }
            catch (Exception)
            {
                ViewData["Title"] = "Job Application";
                return View(new PublicJobApplicationViewModel { LoadFailed = true });
            }

            if (posting == null)
            {
                return NotFound();
            }

            ViewData["Title"] = $"Apply - {posting.JobTitle}";

            var model = new PublicJobApplicationViewModel { JobPosting = posting };
            model.Form.JobPostingId = posting.Id;
            PopulateApplicationOptions(model.Form);

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int id, PublicJobApplicationViewModel model)
        {
            var posting = await LoadPostingHeaderAsync(id);
            if (posting == null)
            {
                return NotFound();
            }

            model.JobPosting = posting;
            ViewData["Title"] = $"Apply - {posting.JobTitle}";

            var form = model.Form;

            // Never trust client-supplied protected values: the posting comes from
            // the route, the date from the server clock, and the status is always
            // the pipeline's initial stage.
            form.Id = null;
            form.ApplicationNumber = null;
            form.JobPostingId = posting.Id;
            form.ApplicationDate = DateOnly.FromDateTime(DateTime.UtcNow);
            form.Status = JobApplicationStatus.Submitted;

            // Drop any validation state produced while binding the values we just
            // replaced, so a tampered request cannot force a spurious failure.
            ModelState.Remove($"{nameof(model.Form)}.{nameof(form.Id)}");
            ModelState.Remove($"{nameof(model.Form)}.{nameof(form.JobPostingId)}");
            ModelState.Remove($"{nameof(model.Form)}.{nameof(form.ApplicationDate)}");

            NormalizeForm(form);

            if (!ModelState.IsValid)
            {
                PopulateApplicationOptions(form);
                return View(model);
            }

            if (await _duplicateService.ExistsAsync(form.JobPostingId, form.Email, form.Phone, form.NationalIdNumber))
            {
                model.DuplicateDetected = true;
                return View(model);
            }

            var cvResult = await _fileService.SavePdfAsync(form.CvFile, CvFolder);
            if (cvResult is not { Success: true })
            {
                ModelState.AddModelError("Form.CvFile", cvResult?.ErrorMessage ?? "CV / Resume could not be uploaded.");
                PopulateApplicationOptions(form);
                return View(model);
            }

            var savedDocuments = new List<(string StoredName, string OriginalName, long Size)>();

            try
            {
                foreach (var document in form.SupportingDocuments.Where(file => file.Length > 0))
                {
                    var result = await _fileService.SavePdfAsync(document, DocumentsFolder);
                    if (result is not { Success: true })
                    {
                        await DeleteUploadedAsync(cvResult.StoredFileName!, savedDocuments);
                        ModelState.AddModelError("Form.SupportingDocuments",
                            result?.ErrorMessage ?? "A supporting document could not be uploaded.");
                        PopulateApplicationOptions(form);
                        return View(model);
                    }

                    savedDocuments.Add((result.StoredFileName!, document.FileName, document.Length));
                }

                var application = new JobApplication
                {
                    ApplicationNumber = await _applicationNumberService.GenerateNextReferenceAsync(),
                    JobPostingId = form.JobPostingId,
                    FirstName = form.FirstName,
                    LastName = form.LastName,
                    Email = form.Email,
                    Phone = form.Phone,
                    NationalIdNumber = form.NationalIdNumber,
                    DateOfBirth = form.DateOfBirth,
                    Gender = form.Gender,
                    City = form.City,
                    Country = form.Country,
                    CurrentAddress = form.CurrentAddress,
                    ApplicationDate = form.ApplicationDate,
                    Status = JobApplicationStatus.Submitted,
                    ExperienceYears = form.ExperienceYears ?? 0,
                    HighestEducation = form.HighestEducation,
                    CurrentEmployer = form.CurrentEmployer,
                    CurrentJobTitle = form.CurrentJobTitle,
                    ExpectedSalary = form.ExpectedSalary,
                    NoticePeriod = form.NoticePeriod,
                    CoverLetter = form.CoverLetter,
                    LinkedInUrl = form.LinkedInUrl,
                    PortfolioUrl = form.PortfolioUrl,
                    CvFileName = cvResult.StoredFileName!,
                    CvOriginalFileName = form.CvFile!.FileName,
                    CvContentType = form.CvFile!.ContentType,
                    CvFileSize = form.CvFile!.Length,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    // Public submissions are not attributed to an internal user.
                    CreatedByUserId = null
                };

                foreach (var (stored, original, size) in savedDocuments)
                {
                    application.Documents.Add(new JobApplicationDocument
                    {
                        StoredFileName = stored,
                        OriginalFileName = original,
                        ContentType = "application/pdf",
                        FileSize = size
                    });
                }

                _dbContext.JobApplications.Add(application);
                await _dbContext.SaveChangesAsync();

                TempData["CareersApplicationReference"] = application.ApplicationNumber;
                TempData["CareersApplicationJobTitle"] = posting.JobTitle;
                TempData["CareersApplicationJobReference"] = posting.ReferenceNumber;

                return RedirectToAction(nameof(ApplySuccess));
            }
            catch (DbUpdateException)
            {
                // Guards the race between the pre-save duplicate check and the
                // unique indexes on (JobPostingId, Email / Phone / NationalIdNumber).
                await DeleteUploadedAsync(cvResult.StoredFileName!, savedDocuments);
                model.DuplicateDetected = true;
                return View(model);
            }
            catch (Exception)
            {
                await DeleteUploadedAsync(cvResult.StoredFileName!, savedDocuments);
                ModelState.AddModelError(string.Empty,
                    "We could not submit your application right now. Please try again in a moment.");
                PopulateApplicationOptions(form);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ApplySuccess()
        {
            var reference = TempData["CareersApplicationReference"] as string;
            if (string.IsNullOrWhiteSpace(reference))
            {
                return RedirectToAction(nameof(Index));
            }

            ViewData["Title"] = "Application Received";
            return View(new PublicJobApplicationSuccessViewModel
            {
                ApplicationNumber = reference,
                JobTitle = TempData["CareersApplicationJobTitle"] as string ?? string.Empty,
                ReferenceNumber = TempData["CareersApplicationJobReference"] as string ?? string.Empty
            });
        }

        /// <summary>
        /// The single source of truth for "publicly visible" postings: active,
        /// open, and still inside the application deadline.
        /// </summary>
        private IQueryable<JobPosting> EligiblePostings()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return _dbContext.JobPostings
                .AsNoTracking()
                .Where(posting => posting.IsActive
                    && posting.Status == JobPostingStatus.Open
                    && posting.ApplicationDeadline >= today);
        }

        private async Task<PublicJobPostingViewModel?> LoadPostingHeaderAsync(int id)
        {
            return await EligiblePostings()
                .Where(posting => posting.Id == id)
                .Select(posting => new PublicJobPostingViewModel
                {
                    Id = posting.Id,
                    ReferenceNumber = posting.ReferenceNumber,
                    JobTitle = posting.JobTitle,
                    Department = posting.Department,
                    EmploymentType = posting.EmploymentType,
                    WorkplaceType = posting.WorkplaceType,
                    JobLocation = posting.JobLocation,
                    Vacancies = posting.Vacancies,
                    PostedOn = DateOnly.FromDateTime(posting.CreatedAt),
                    ApplicationDeadline = posting.ApplicationDeadline
                })
                .FirstOrDefaultAsync();
        }

        private static void PopulateApplicationOptions(JobApplicationFormViewModel form)
        {
            form.Countries = HRLists.Countries.Select(country => new SelectListItem(country, country)).ToList();
            form.EducationLevels = HRLists.EducationLevels.Select(level => new SelectListItem(level, level)).ToList();
            form.NoticePeriods = HRLists.NoticePeriods.Select(period => new SelectListItem(period, period)).ToList();
        }

        /// <summary>
        /// Mirrors the HR application normalization so stored values are identical
        /// regardless of whether an application arrives publicly or through HR.
        /// </summary>
        private static void NormalizeForm(JobApplicationFormViewModel form)
        {
            form.FirstName = TrimRequired(form.FirstName);
            form.LastName = TrimRequired(form.LastName);
            form.Email = CandidateContactNormalizer.NormalizeEmail(form.Email);
            form.Phone = CandidateContactNormalizer.NormalizePhone(form.Phone);
            if (NationalIdValidator.TryNormalize(form.NationalIdNumber, form.Country, out var nationalId, out _))
            {
                form.NationalIdNumber = nationalId;
            }

            form.City = TrimRequired(form.City);
            form.Country = string.IsNullOrWhiteSpace(form.Country) ? "Pakistan" : form.Country.Trim();
            form.CurrentAddress = TrimOptional(form.CurrentAddress);
            form.CurrentEmployer = TrimOptional(form.CurrentEmployer);
            form.CurrentJobTitle = TrimOptional(form.CurrentJobTitle);
            form.HighestEducation = TrimRequired(form.HighestEducation);
            form.NoticePeriod = TrimOptional(form.NoticePeriod);
            form.CoverLetter = TrimOptional(form.CoverLetter);
            form.LinkedInUrl = TrimOptional(form.LinkedInUrl);
            form.PortfolioUrl = TrimOptional(form.PortfolioUrl);
        }

        private async Task DeleteUploadedAsync(
            string? cvStoredName,
            List<(string StoredName, string OriginalName, long Size)> documents)
        {
            if (!string.IsNullOrWhiteSpace(cvStoredName))
            {
                await _fileService.DeleteAsync(cvStoredName, CvFolder);
            }

            foreach (var (stored, _, _) in documents)
            {
                await _fileService.DeleteAsync(stored, DocumentsFolder);
            }
        }

        private static string BuildDescriptionPreview(string description)
        {
            var text = description.Trim();

            if (text.Length <= DescriptionPreviewLength)
            {
                return text;
            }

            var preview = text.Substring(0, DescriptionPreviewLength);
            var lastSpace = preview.LastIndexOf(' ');

            if (lastSpace > DescriptionPreviewLength / 2)
            {
                preview = preview.Substring(0, lastSpace);
            }

            return preview.TrimEnd() + "...";
        }

        private static string TrimRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
