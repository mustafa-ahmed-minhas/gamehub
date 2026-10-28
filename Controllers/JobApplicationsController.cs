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
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class JobApplicationsController : Controller
    {
        private const string CvFolder = "cvs";
        private const string DocumentsFolder = "documents";

        private readonly ApplicationDbContext _dbContext;
        private readonly IJobApplicationNumberService _applicationNumberService;
        private readonly IJobApplicationFileService _fileService;
        private readonly IJobApplicationDuplicateService _duplicateService;
        private readonly ILogger<JobApplicationsController> _logger;

        public JobApplicationsController(
            ApplicationDbContext dbContext,
            IJobApplicationNumberService applicationNumberService,
            IJobApplicationFileService fileService,
            IJobApplicationDuplicateService duplicateService,
            ILogger<JobApplicationsController> logger)
        {
            _dbContext = dbContext;
            _applicationNumberService = applicationNumberService;
            _fileService = fileService;
            _duplicateService = duplicateService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var applications = await _dbContext.JobApplications
                .AsNoTracking()
                .Include(application => application.JobPosting)
                .Include(application => application.Documents)
                .OrderByDescending(application => application.CreatedAt)
                .ToListAsync();

            var newApplications = applications.Count(application => application.Status == JobApplicationStatus.Submitted);
            var inProcess = applications.Count(application =>
                application.Status is JobApplicationStatus.Screening
                    or JobApplicationStatus.Interview
                    or JobApplicationStatus.Offer);
            var hired = applications.Count(application => application.Status == JobApplicationStatus.Hired);

            return View(new JobApplicationIndexViewModel
            {
                TotalApplications = applications.Count,
                NewApplications = newApplications,
                InProcess = inProcess,
                Hired = hired,
                CanManage = CanManage(),
                Applications = applications
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? jobPostingId)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var model = new JobApplicationFormViewModel
            {
                ApplicationNumber = await _applicationNumberService.GenerateNextReferenceAsync()
            };

            if (jobPostingId.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var eligible = await _dbContext.JobPostings.AsNoTracking().AnyAsync(posting =>
                    posting.Id == jobPostingId.Value
                    && posting.IsActive
                    && posting.Status == JobPostingStatus.Open
                    && posting.ApplicationDeadline >= today);

                if (eligible)
                {
                    model.JobPostingId = jobPostingId.Value;
                }
            }

            return View("Form", await PopulateOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JobApplicationFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            model.ApplicationNumber = await _applicationNumberService.GenerateNextReferenceAsync();
            await ValidateApplicationAsync(model);

            if (!ModelState.IsValid)
            {
                return View("Form", await PopulateOptionsAsync(model));
            }

            var cvResult = await _fileService.SavePdfAsync(model.CvFile, CvFolder);
            if (cvResult is not { Success: true })
            {
                ModelState.AddModelError(nameof(model.CvFile), cvResult?.ErrorMessage ?? "CV / Resume could not be uploaded.");
                return View("Form", await PopulateOptionsAsync(model));
            }

            var savedDocuments = new List<(string StoredName, string OriginalName, long Size)>();
            try
            {
                foreach (var document in model.SupportingDocuments.Where(file => file.Length > 0))
                {
                    var result = await _fileService.SavePdfAsync(document, DocumentsFolder);
                    if (result is not { Success: true })
                    {
                        await _fileService.DeleteAsync(cvResult.StoredFileName!, CvFolder);
                        foreach (var (stored, _, _) in savedDocuments)
                        {
                            await _fileService.DeleteAsync(stored, DocumentsFolder);
                        }

                        ModelState.AddModelError(nameof(model.SupportingDocuments),
                            result?.ErrorMessage ?? "A supporting document could not be uploaded.");
                        return View("Form", await PopulateOptionsAsync(model));
                    }

                    savedDocuments.Add((result.StoredFileName!, document.FileName, document.Length));
                }

                var application = new JobApplication();
                MapToEntity(model, application, cvResult.StoredFileName!, model.CvFile!.FileName, model.CvFile.ContentType, model.CvFile.Length, savedDocuments);
                application.ApplicationNumber = await _applicationNumberService.GenerateNextReferenceAsync();
                application.CreatedAt = DateTime.UtcNow;
                application.CreatedByUserId = CurrentUserId();
                application.IsActive = true;

                _dbContext.JobApplications.Add(application);
                await _dbContext.SaveChangesAsync();

                TempData.SetToast("success", "Success", $"Application {application.ApplicationNumber} submitted successfully.");
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                // Guards the race between the pre-save duplicate check and the unique DB indexes.
                _logger.LogError(ex, "Failed to create job application for posting {JobPostingId}.", model.JobPostingId);
                await _fileService.DeleteAsync(cvResult.StoredFileName!, CvFolder);
                foreach (var (stored, _, _) in savedDocuments)
                {
                    await _fileService.DeleteAsync(stored, DocumentsFolder);
                }

                AddApplicationConstraintErrors(ex);
                return View("Form", await PopulateOptionsAsync(model));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var application = await _dbContext.JobApplications
                .AsNoTracking()
                .Include(entity => entity.JobPosting)
                .Include(entity => entity.Documents)
                .Include(entity => entity.Interviews)
                .Include(entity => entity.CreatedByUser)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (application == null)
            {
                return NotFound();
            }

            var interviews = application.Interviews
                .Where(interview => interview.IsActive)
                .OrderBy(interview => interview.InterviewDate)
                .ThenBy(interview => interview.InterviewTime)
                .ToList();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = TimeOnly.FromDateTime(DateTime.UtcNow);
            var nextInterview = interviews.FirstOrDefault(interview =>
                interview.Status == InterviewStatus.Scheduled
                && (interview.InterviewDate > today
                    || (interview.InterviewDate == today && interview.InterviewTime >= now)));

            return View(new JobApplicationDetailsViewModel
            {
                Application = application,
                Initials = Initials(application.FirstName, application.LastName),
                CalculatedAge = application.DateOfBirth.HasValue ? CalculateAge(application.DateOfBirth.Value) : null,
                CanManage = CanManage(),
                Interviews = interviews,
                NextInterview = nextInterview
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var application = await _dbContext.JobApplications
                .AsNoTracking()
                .Include(entity => entity.Documents)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (application == null)
            {
                return NotFound();
            }

            var model = new JobApplicationFormViewModel
            {
                Id = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                JobPostingId = application.JobPostingId,
                ApplicationDate = application.ApplicationDate,
                Status = application.Status,
                FirstName = application.FirstName,
                LastName = application.LastName,
                DateOfBirth = application.DateOfBirth,
                Gender = application.Gender,
                City = application.City,
                Country = application.Country,
                CurrentAddress = application.CurrentAddress,
                Email = application.Email,
                Phone = application.Phone,
                NationalIdNumber = application.NationalIdNumber,
                ExperienceYears = application.ExperienceYears,
                HighestEducation = application.HighestEducation,
                CurrentEmployer = application.CurrentEmployer,
                CurrentJobTitle = application.CurrentJobTitle,
                ExpectedSalary = application.ExpectedSalary,
                NoticePeriod = application.NoticePeriod,
                CoverLetter = application.CoverLetter,
                LinkedInUrl = application.LinkedInUrl,
                PortfolioUrl = application.PortfolioUrl,
                ExistingCvFileName = application.CvOriginalFileName,
                ExistingCvFileSize = application.CvFileSize,
                ExistingDocuments = application.Documents.OrderBy(document => document.Id).ToList()
            };

            return View("Form", await PopulateOptionsAsync(model, application.JobPostingId));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, JobApplicationFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            if (model.Id != id)
            {
                return NotFound();
            }

            var application = await _dbContext.JobApplications
                .Include(entity => entity.Documents)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);

            if (application == null)
            {
                return NotFound();
            }

            // The CV is optional when editing; the stored file is kept when no new one is supplied.
            if (model.CvFile == null)
            {
                ModelState.Remove(nameof(model.CvFile));
            }

            await ValidateApplicationAsync(model, application);

            if (!ModelState.IsValid)
            {
                await RefillEditContextAsync(model, application);
                return View("Form", model);
            }

            string? newCvStoredName = null;
            var savedDocuments = new List<(string StoredName, string OriginalName, long Size)>();
            try
            {
                if (model.CvFile != null)
                {
                    var cvResult = await _fileService.SavePdfAsync(model.CvFile, CvFolder);
                    if (cvResult is not { Success: true })
                    {
                        ModelState.AddModelError(nameof(model.CvFile), cvResult?.ErrorMessage ?? "CV / Resume could not be uploaded.");
                        await RefillEditContextAsync(model, application);
                        return View("Form", model);
                    }

                    newCvStoredName = cvResult.StoredFileName;
                }

                foreach (var document in model.SupportingDocuments.Where(file => file.Length > 0))
                {
                    var result = await _fileService.SavePdfAsync(document, DocumentsFolder);
                    if (result is not { Success: true })
                    {
                        await DeleteUploadedAsync(newCvStoredName, savedDocuments);
                        ModelState.AddModelError(nameof(model.SupportingDocuments),
                            result?.ErrorMessage ?? "A supporting document could not be uploaded.");
                        await RefillEditContextAsync(model, application);
                        return View("Form", model);
                    }

                    savedDocuments.Add((result.StoredFileName!, document.FileName, document.Length));
                }

                var previousCv = application.CvFileName;
                var cvStored = newCvStoredName ?? application.CvFileName;
                var cvOriginal = newCvStoredName != null
                    ? model.CvFile!.FileName
                    : application.CvOriginalFileName ?? $"CV-{application.ApplicationNumber}.pdf";
                var cvContentType = newCvStoredName != null
                    ? model.CvFile!.ContentType
                    : application.CvContentType ?? "application/pdf";
                var cvSize = newCvStoredName != null ? model.CvFile!.Length : application.CvFileSize;

                MapToEntity(model, application, cvStored, cvOriginal, cvContentType, cvSize, savedDocuments, isCreate: false);
                application.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                if (newCvStoredName != null && newCvStoredName != previousCv)
                {
                    await _fileService.DeleteAsync(previousCv, CvFolder);
                }

                TempData.SetToast("success", "Success", $"Application {application.ApplicationNumber} updated successfully.");
                return RedirectToAction(nameof(Details), new { id = application.Id });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update job application {ApplicationId}.", id);
                await DeleteUploadedAsync(newCvStoredName, savedDocuments);
                AddApplicationConstraintErrors(ex);
                await RefillEditContextAsync(model, application);
                return View("Form", model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Shortlist(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var application = await _dbContext.JobApplications
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (application == null)
            {
                return NotFound();
            }

            if (application.Status is JobApplicationStatus.Hired or JobApplicationStatus.Rejected)
            {
                TempData.SetToast("warning", "Not Allowed",
                    "Shortlisting is only available for applications that are still in review.");
            }
            else
            {
                application.Status = JobApplicationStatus.Shortlisted;
                application.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await _dbContext.SaveChangesAsync();
                    TempData.SetToast("success", "Success",
                        $"{application.FirstName} {application.LastName} has been shortlisted for {application.ApplicationNumber}.");
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to shortlist job application {ApplicationId}.", id);
                    TempData.SetToast("error", "Update Failed", "The application could not be shortlisted. Please try again.");
                }
            }

            return SafeRedirect(returnUrl, application.Id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? returnUrl = null)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var application = await _dbContext.JobApplications
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (application == null)
            {
                return NotFound();
            }

            if (application.Status == JobApplicationStatus.Hired)
            {
                TempData.SetToast("warning", "Not Allowed", "A hired candidate cannot be rejected.");
            }
            else
            {
                application.Status = JobApplicationStatus.Rejected;
                application.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await _dbContext.SaveChangesAsync();
                    TempData.SetToast("success", "Success",
                        $"{application.FirstName} {application.LastName} has been rejected for {application.ApplicationNumber}.");
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to reject job application {ApplicationId}.", id);
                    TempData.SetToast("error", "Update Failed", "The application could not be rejected. Please try again.");
                }
            }

            return SafeRedirect(returnUrl, application.Id);
        }

        [HttpGet]
        public async Task<IActionResult> ScheduleInterview(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var application = await _dbContext.JobApplications
                .AsNoTracking()
                .Include(entity => entity.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (application == null)
            {
                return NotFound();
            }

            var model = new InterviewFormViewModel
            {
                JobApplicationId = application.Id,
                ApplicationNumber = application.ApplicationNumber,
                CandidateName = $"{application.FirstName} {application.LastName}".Trim(),
                JobTitle = application.JobPosting?.JobTitle ?? string.Empty,
                InterviewDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                InterviewTime = new TimeOnly(10, 0),
                InterviewType = InterviewType.InPerson,
                Status = InterviewStatus.Scheduled,
                Outcome = InterviewOutcome.Pending
            };

            return View("InterviewForm", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleInterview(InterviewFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var application = await _dbContext.JobApplications
                .Include(entity => entity.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == model.JobApplicationId && entity.IsActive);
            if (application == null)
            {
                return NotFound();
            }

            FillInterviewContext(model, application);

            if (application.Status is JobApplicationStatus.Hired or JobApplicationStatus.Rejected)
            {
                TempData.SetToast("warning", "Not Allowed",
                    "Interviews cannot be scheduled for a hired or rejected application.");
                return RedirectToAction(nameof(Details), new { id = application.Id });
            }

            if (!ModelState.IsValid)
            {
                return View("InterviewForm", model);
            }

            if (await InterviewSlotExistsAsync(application.Id, model.InterviewDate!.Value, model.InterviewTime!.Value))
            {
                ModelState.AddModelError(nameof(model.InterviewTime),
                    "An interview for this candidate is already scheduled at that date and time.");
                return View("InterviewForm", model);
            }

            var interview = new Interview
            {
                JobApplicationId = application.Id,
                InterviewDate = model.InterviewDate!.Value,
                InterviewTime = model.InterviewTime!.Value,
                InterviewType = model.InterviewType,
                Interviewer = model.Interviewer.Trim(),
                Location = TrimOptional(model.Location),
                Notes = TrimOptional(model.Notes),
                Status = InterviewStatus.Scheduled,
                Outcome = InterviewOutcome.Pending,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = CurrentUserId(),
                IsActive = true
            };

            _dbContext.Interviews.Add(interview);

            // Advance the pipeline into the interview stage when still in review.
            if (application.Status is JobApplicationStatus.Submitted
                or JobApplicationStatus.Screening
                or JobApplicationStatus.Shortlisted)
            {
                application.Status = JobApplicationStatus.Interview;
            }

            application.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to schedule interview for application {ApplicationId}.", application.Id);
                ModelState.AddModelError(string.Empty, "The interview could not be scheduled. Please try again.");
                return View("InterviewForm", model);
            }

            TempData.SetToast("success", "Success",
                $"Interview scheduled for {application.FirstName} {application.LastName} on {interview.InterviewDate:dd MMM yyyy} at {interview.InterviewTime:HH\\:mm}.");
            return RedirectToAction(nameof(Details), new { id = application.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditInterview(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var interview = await _dbContext.Interviews
                .AsNoTracking()
                .Include(entity => entity.JobApplication)
                .ThenInclude(application => application!.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (interview == null)
            {
                return NotFound();
            }

            var application = interview.JobApplication;
            return View("InterviewForm", new InterviewFormViewModel
            {
                Id = interview.Id,
                JobApplicationId = interview.JobApplicationId,
                ApplicationNumber = application?.ApplicationNumber ?? string.Empty,
                CandidateName = application == null ? string.Empty : $"{application.FirstName} {application.LastName}".Trim(),
                JobTitle = application?.JobPosting?.JobTitle ?? string.Empty,
                InterviewDate = interview.InterviewDate,
                InterviewTime = interview.InterviewTime,
                InterviewType = interview.InterviewType,
                Interviewer = interview.Interviewer,
                Location = interview.Location,
                Notes = interview.Notes,
                Status = interview.Status,
                Outcome = interview.Outcome,
                Feedback = interview.Feedback
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditInterview(int id, InterviewFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            if (model.Id != id)
            {
                return NotFound();
            }

            var interview = await _dbContext.Interviews
                .Include(entity => entity.JobApplication)
                .ThenInclude(application => application!.JobPosting)
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (interview == null)
            {
                return NotFound();
            }

            if (interview.JobApplication != null)
            {
                FillInterviewContext(model, interview.JobApplication);
            }

            if (!ModelState.IsValid)
            {
                return View("InterviewForm", model);
            }

            if (await InterviewSlotExistsAsync(interview.JobApplicationId, model.InterviewDate!.Value, model.InterviewTime!.Value, interview.Id))
            {
                ModelState.AddModelError(nameof(model.InterviewTime),
                    "An interview for this candidate is already scheduled at that date and time.");
                return View("InterviewForm", model);
            }

            interview.InterviewDate = model.InterviewDate!.Value;
            interview.InterviewTime = model.InterviewTime!.Value;
            interview.InterviewType = model.InterviewType;
            interview.Interviewer = model.Interviewer.Trim();
            interview.Location = TrimOptional(model.Location);
            interview.Notes = TrimOptional(model.Notes);
            interview.Status = model.Status;
            interview.Outcome = model.Outcome;
            interview.Feedback = TrimOptional(model.Feedback);
            interview.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update interview {InterviewId}.", id);
                ModelState.AddModelError(string.Empty, "The interview could not be updated. Please try again.");
                return View("InterviewForm", model);
            }

            TempData.SetToast("success", "Success", "Interview updated successfully.");
            return RedirectToAction(nameof(Details), new { id = interview.JobApplicationId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateInterview(int id, InterviewStatus status, InterviewOutcome outcome, string? feedback)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var interview = await _dbContext.Interviews
                .FirstOrDefaultAsync(entity => entity.Id == id && entity.IsActive);
            if (interview == null)
            {
                return NotFound();
            }

            if (!Enum.IsDefined(status) || !Enum.IsDefined(outcome))
            {
                TempData.SetToast("warning", "Invalid Selection", "Select a valid interview status and outcome.");
                return RedirectToAction(nameof(Details), new { id = interview.JobApplicationId });
            }

            if (status == InterviewStatus.Completed && outcome == InterviewOutcome.Pending)
            {
                TempData.SetToast("warning", "Outcome Required",
                    "Select an interview outcome before marking the interview as completed.");
                return RedirectToAction(nameof(Details), new { id = interview.JobApplicationId });
            }

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var nowTime = TimeOnly.FromDateTime(now);
            var isScheduledInFuture = interview.InterviewDate > today
                || (interview.InterviewDate == today && interview.InterviewTime > nowTime);

            if (isScheduledInFuture && status is InterviewStatus.Completed or InterviewStatus.NoShow)
            {
                TempData.SetToast("warning", "Not Allowed",
                    "An interview cannot be marked completed or no-show before its scheduled time.");
                return RedirectToAction(nameof(Details), new { id = interview.JobApplicationId });
            }

            interview.Status = status;
            interview.Outcome = outcome;
            interview.Feedback = TrimOptional(feedback);
            interview.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update interview status for {InterviewId}.", id);
                TempData.SetToast("error", "Update Failed", "The interview status could not be updated. Please try again.");
                return RedirectToAction(nameof(Details), new { id = interview.JobApplicationId });
            }

            TempData.SetToast("success", "Success", "Interview status and outcome updated.");
            return RedirectToAction(nameof(Details), new { id = interview.JobApplicationId });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadCv(int id)
        {
            if (!CanViewDocuments()) return NotFound();

            var application = await _dbContext.JobApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (application == null || string.IsNullOrWhiteSpace(application.CvFileName))
            {
                return NotFound();
            }

            try
            {
                var bytes = await _fileService.ReadAllBytesAsync(application.CvFileName, CvFolder);
                var displayName = string.IsNullOrWhiteSpace(application.CvOriginalFileName)
                    ? $"CV-{application.ApplicationNumber}.pdf"
                    : application.CvOriginalFileName;
                return File(bytes, application.CvContentType ?? "application/pdf", displayName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            if (!CanViewDocuments()) return NotFound();

            var document = await _dbContext.JobApplicationDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
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

        private async Task ValidateApplicationAsync(JobApplicationFormViewModel model, JobApplication? exclude = null)
        {
            NormalizeForm(model);

            if (model.JobPostingId <= 0)
            {
                ModelState.AddModelError(nameof(model.JobPostingId), "Select a job posting.");
                return;
            }

            var posting = await _dbContext.JobPostings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == model.JobPostingId);

            if (posting == null || !posting.IsActive)
            {
                ModelState.AddModelError(nameof(model.JobPostingId), "The selected job posting is no longer available.");
                return;
            }

            // New applications require an open posting. When editing, the application's
            // own (possibly closed) posting is still allowed, but switching to another
            // posting that is closed or past its deadline is rejected.
            var isSamePosting = exclude != null && exclude.JobPostingId == posting.Id;
            if (!isSamePosting
                && (posting.Status != JobPostingStatus.Open
                    || posting.ApplicationDeadline < DateOnly.FromDateTime(DateTime.UtcNow)))
            {
                ModelState.AddModelError(nameof(model.JobPostingId),
                    "This job posting is not currently accepting applications.");
            }

            // Abide by the shared rule used by both the public careers form and the
            // HR screens: one application per person per posting, matched on
            // normalized email, normalized phone number, or national ID.
            var duplicate = await _duplicateService.ExistsAsync(
                model.JobPostingId,
                model.Email,
                model.Phone,
                model.NationalIdNumber,
                exclude?.Id);

            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.Email),
                    "An application for this job with the same email, phone number, or National ID already exists.");
                ModelState.AddModelError(string.Empty,
                    "A duplicate application for this job was detected. Only one application per person per job is allowed.");
            }

            if (!string.IsNullOrWhiteSpace(model.Country) && !HRLists.Countries.Contains(model.Country))
            {
                ModelState.AddModelError(nameof(model.Country), "Select a country from the list.");
            }

            if (!string.IsNullOrWhiteSpace(model.HighestEducation) && !HRLists.EducationLevels.Contains(model.HighestEducation))
            {
                ModelState.AddModelError(nameof(model.HighestEducation), "Select an education level from the list.");
            }

            if (!string.IsNullOrWhiteSpace(model.NoticePeriod) && !HRLists.NoticePeriods.Contains(model.NoticePeriod))
            {
                ModelState.AddModelError(nameof(model.NoticePeriod), "Select a notice period from the list.");
            }
        }

        private Task<bool> InterviewSlotExistsAsync(int applicationId, DateOnly date, TimeOnly time, int? excludeInterviewId = null)
        {
            return _dbContext.Interviews.AnyAsync(interview =>
                interview.IsActive
                && interview.JobApplicationId == applicationId
                && interview.InterviewDate == date
                && interview.InterviewTime == time
                && interview.Status != InterviewStatus.Cancelled
                && (!excludeInterviewId.HasValue || interview.Id != excludeInterviewId.Value));
        }

        private void AddApplicationConstraintErrors(DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            const string duplicateMessage =
                "An application for this job with the same email, phone number, or National ID already exists. Duplicate applications are not allowed.";

            if (detail.Contains("IX_JobApplications_JobPostingId_Email", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("IX_JobApplications_JobPostingId_Phone", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("IX_JobApplications_JobPostingId_NationalIdNumber", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(JobApplicationFormViewModel.Email), duplicateMessage);
                ModelState.AddModelError(string.Empty, duplicateMessage);
                TempData.SetToast("error", "Duplicate Application", duplicateMessage);
            }
            else if (detail.Contains("IX_JobApplications_ApplicationNumber", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "A conflicting application number was generated. Please submit the form again.");
                TempData.SetToast("error", "Please Retry", "A conflicting application number was generated. Submit the form again.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "The application could not be saved due to a data conflict. Please review the details and try again.");
                TempData.SetToast("error", "Save Failed", "The application could not be saved. Please try again.");
            }
        }

        private void NormalizeForm(JobApplicationFormViewModel model)
        {
            model.FirstName = TrimRequired(model.FirstName);
            model.LastName = TrimRequired(model.LastName);
            model.Email = TrimRequired(model.Email).ToLowerInvariant();
            model.Phone = CandidateContactNormalizer.NormalizePhone(model.Phone);
            if (NationalIdValidator.TryNormalize(model.NationalIdNumber, model.Country, out var nationalId, out _))
            {
                model.NationalIdNumber = nationalId;
            }

            model.City = TrimRequired(model.City);
            model.Country = string.IsNullOrWhiteSpace(model.Country) ? "Pakistan" : model.Country.Trim();
            model.CurrentAddress = TrimOptional(model.CurrentAddress);
            model.CurrentEmployer = TrimOptional(model.CurrentEmployer);
            model.CurrentJobTitle = TrimOptional(model.CurrentJobTitle);
            model.HighestEducation = TrimRequired(model.HighestEducation);
            model.NoticePeriod = TrimOptional(model.NoticePeriod);
            model.CoverLetter = TrimOptional(model.CoverLetter);
            model.LinkedInUrl = TrimOptional(model.LinkedInUrl);
            model.PortfolioUrl = TrimOptional(model.PortfolioUrl);
        }

        private static void MapToEntity(
            JobApplicationFormViewModel model,
            JobApplication application,
            string cvStoredName,
            string cvOriginalName,
            string cvContentType,
            long cvFileSize,
            List<(string StoredName, string OriginalName, long Size)> documents,
            bool isCreate = true)
        {
            application.JobPostingId = model.JobPostingId;
            application.FirstName = model.FirstName;
            application.LastName = model.LastName;
            application.Email = model.Email;
            application.Phone = model.Phone;
            application.NationalIdNumber = model.NationalIdNumber;
            application.DateOfBirth = model.DateOfBirth;
            application.Gender = model.Gender;
            application.City = model.City;
            application.Country = model.Country;
            application.CurrentAddress = model.CurrentAddress;
            application.ApplicationDate = model.ApplicationDate <= DateOnly.FromDateTime(DateTime.UtcNow)
                ? model.ApplicationDate
                : DateOnly.FromDateTime(DateTime.UtcNow);
            if (isCreate)
            {
                application.Status = JobApplicationStatus.Submitted;
            }

            application.ExperienceYears = model.ExperienceYears ?? 0;
            application.HighestEducation = model.HighestEducation;
            application.CurrentEmployer = model.CurrentEmployer;
            application.CurrentJobTitle = model.CurrentJobTitle;
            application.ExpectedSalary = model.ExpectedSalary;
            application.NoticePeriod = model.NoticePeriod;
            application.CoverLetter = model.CoverLetter;
            application.LinkedInUrl = model.LinkedInUrl;
            application.PortfolioUrl = model.PortfolioUrl;
            application.CvFileName = cvStoredName;
            application.CvOriginalFileName = cvOriginalName;
            application.CvContentType = cvContentType;
            application.CvFileSize = cvFileSize;

            foreach (var (stored, original, size) in documents)
            {
                application.Documents.Add(new JobApplicationDocument
                {
                    StoredFileName = stored,
                    OriginalFileName = original,
                    ContentType = "application/pdf",
                    FileSize = size
                });
            }
        }

        private async Task<JobApplicationFormViewModel> PopulateOptionsAsync(JobApplicationFormViewModel model, int? includePostingId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var available = await _dbContext.JobPostings
                .AsNoTracking()
                .Where(posting => posting.IsActive
                    && ((posting.Status == JobPostingStatus.Open && posting.ApplicationDeadline >= today)
                        || (includePostingId.HasValue && posting.Id == includePostingId.Value)))
                .OrderByDescending(posting => posting.CreatedAt)
                .Select(posting => new { posting.Id, posting.ReferenceNumber, posting.JobTitle, posting.Department })
                .ToListAsync();

            model.AvailableJobPostings = available
                .Select(posting => new SelectListItem(
                    $"{posting.JobTitle} ({posting.ReferenceNumber} - {posting.Department})",
                    posting.Id.ToString(),
                    posting.Id == model.JobPostingId))
                .ToList();
            model.Countries = HRLists.Countries.Select(country => new SelectListItem(country, country)).ToList();
            model.EducationLevels = HRLists.EducationLevels.Select(level => new SelectListItem(level, level)).ToList();
            model.NoticePeriods = HRLists.NoticePeriods.Select(period => new SelectListItem(period, period)).ToList();
            return model;
        }

        private async Task RefillEditContextAsync(JobApplicationFormViewModel model, JobApplication application)
        {
            model.ApplicationNumber = application.ApplicationNumber;
            model.ExistingCvFileName = application.CvOriginalFileName;
            model.ExistingCvFileSize = application.CvFileSize;
            model.ExistingDocuments = application.Documents.OrderBy(document => document.Id).ToList();
            await PopulateOptionsAsync(model, application.JobPostingId);
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

        private static void FillInterviewContext(InterviewFormViewModel model, JobApplication application)
        {
            model.ApplicationNumber = application.ApplicationNumber;
            model.CandidateName = $"{application.FirstName} {application.LastName}".Trim();
            model.JobTitle = application.JobPosting?.JobTitle ?? string.Empty;
        }

        private IActionResult SafeRedirect(string? returnUrl, int applicationId)
            => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(nameof(Details), new { id = applicationId });

        private static string Initials(string firstName, string lastName)
        {
            var first = firstName.FirstOrDefault();
            var last = lastName.FirstOrDefault();
            var initials = $"{first}{last}".Trim();
            return string.IsNullOrWhiteSpace(initials) ? "NA" : initials.ToUpperInvariant();
        }

        private static int CalculateAge(DateOnly dateOfBirth)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.AddYears(age) > today)
            {
                age--;
            }

            return Math.Max(0, age);
        }

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        /// <summary>
        /// CVs contain sensitive applicant information and are only exposed to HR management roles.
        /// </summary>
        private bool CanViewDocuments() => CanManage();

        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private static string TrimRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}