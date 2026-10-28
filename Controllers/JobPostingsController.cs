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
    public class JobPostingsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IJobPostingNumberService _jobPostingNumberService;
        private readonly ILogger<JobPostingsController> _logger;

        public JobPostingsController(ApplicationDbContext dbContext, IJobPostingNumberService jobPostingNumberService, ILogger<JobPostingsController> logger)
        {
            _dbContext = dbContext;
            _jobPostingNumberService = jobPostingNumberService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expiresSoonDate = today.AddDays(14);

            var postings = await _dbContext.JobPostings
                .AsNoTracking()
                .OrderByDescending(posting => posting.CreatedAt)
                .ToListAsync();

            var openCount = await _dbContext.JobPostings
                .AsNoTracking()
                .CountAsync(posting => posting.Status == JobPostingStatus.Open && posting.ApplicationDeadline >= today);
            var applicationsCount = await _dbContext.JobApplications.AsNoTracking().CountAsync();
            var expiringSoon = await _dbContext.JobPostings
                .AsNoTracking()
                .CountAsync(posting => posting.Status == JobPostingStatus.Open
                    && posting.ApplicationDeadline >= today
                    && posting.ApplicationDeadline <= expiresSoonDate);

            return View(new JobPostingIndexViewModel
            {
                TotalPostings = postings.Count,
                OpenPostings = openCount,
                TotalApplications = applicationsCount,
                ExpiringSoon = expiringSoon,
                CanManage = CanManage(),
                Postings = postings
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var model = new JobPostingFormViewModel
            {
                ReferenceNumber = await _jobPostingNumberService.GenerateNextReferenceAsync()
            };

            return View("Form", await PopulateOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JobPostingFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            await ValidatePostingAsync(model);

            if (!ModelState.IsValid)
            {
                model.ReferenceNumber = await _jobPostingNumberService.GenerateNextReferenceAsync();
                return View("Form", await PopulateOptionsAsync(model));
            }

            var posting = new JobPosting();
            MapToEntity(model, posting, isCreate: true);
            posting.ReferenceNumber = await _jobPostingNumberService.GenerateNextReferenceAsync();
            posting.CreatedAt = DateTime.UtcNow;
            posting.CreatedByUserId = CurrentUserId();
            posting.IsActive = true;

            _dbContext.JobPostings.Add(posting);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to create job posting {Reference}.", posting.ReferenceNumber);
                AddPostingConstraintError(ex);
                return View("Form", await PopulateOptionsAsync(model));
            }

            TempData.SetToast("success", "Success", $"Job posting {posting.ReferenceNumber} created successfully.");
            return RedirectToAction(nameof(Details), new { id = posting.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var posting = await _dbContext.JobPostings
                .AsNoTracking()
                .Include(entity => entity.Skills)
                .Include(entity => entity.ScreeningQuestions)
                .Include(entity => entity.HiringManager)
                .Include(entity => entity.CreatedByUser)
                .Include(entity => entity.Applications)
                .FirstOrDefaultAsync(entity => entity.Id == id);

            if (posting == null)
            {
                return NotFound();
            }

            return View(new JobPostingDetailsViewModel
            {
                Posting = posting,
                Applications = posting.Applications
                    .Where(application => application.IsActive)
                    .OrderByDescending(application => application.CreatedAt)
                    .ToList(),
                CanManage = CanManage()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            var posting = await _dbContext.JobPostings
                .AsNoTracking()
                .Include(entity => entity.Skills)
                .Include(entity => entity.ScreeningQuestions)
                .FirstOrDefaultAsync(entity => entity.Id == id);

            if (posting == null)
            {
                return NotFound();
            }

            var model = new JobPostingFormViewModel
            {
                Id = posting.Id,
                ReferenceNumber = posting.ReferenceNumber,
                JobTitle = posting.JobTitle,
                Department = posting.Department,
                EmploymentType = posting.EmploymentType,
                WorkplaceType = posting.WorkplaceType,
                JobLocation = posting.JobLocation,
                Vacancies = posting.Vacancies,
                MinimumSalary = posting.MinimumSalary,
                MaximumSalary = posting.MaximumSalary,
                Currency = posting.Currency,
                ExperienceRequired = posting.ExperienceRequired,
                MinimumEducation = posting.MinimumEducation,
                JobDescription = posting.JobDescription,
                KeyResponsibilities = posting.KeyResponsibilities,
                Qualifications = posting.Qualifications,
                ApplicationDeadline = posting.ApplicationDeadline,
                Status = posting.Status,
                HiringManagerUserId = posting.HiringManagerUserId,
                Benefits = posting.Benefits,
                RequiredSkills = posting.Skills.OrderBy(skill => skill.Id).Select(skill => skill.Skill).ToList(),
                ScreeningQuestions = posting.ScreeningQuestions
                    .OrderBy(question => question.Id)
                    .Select(question => question.Question)
                    .ToList()
            };

            return View("Form", await PopulateOptionsAsync(model));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, JobPostingFormViewModel model)
        {
            if (EnsureCanManage() is IActionResult blocked) return blocked;

            if (model.Id != id)
            {
                return NotFound();
            }

            var posting = await _dbContext.JobPostings
                .Include(entity => entity.Skills)
                .Include(entity => entity.ScreeningQuestions)
                .FirstOrDefaultAsync(entity => entity.Id == id);

            if (posting == null)
            {
                return NotFound();
            }

            // A posting may already carry a past deadline. Editing other fields
            // must not force the deadline to be moved into the future.
            if (model.ApplicationDeadline.HasValue && model.ApplicationDeadline.Value == posting.ApplicationDeadline)
            {
                ModelState.Remove(nameof(model.ApplicationDeadline));
            }

            await ValidatePostingAsync(model, excludeId: id);

            if (!ModelState.IsValid)
            {
                model.ReferenceNumber = posting.ReferenceNumber;
                return View("Form", await PopulateOptionsAsync(model));
            }

            posting.Skills.Clear();
            posting.ScreeningQuestions.Clear();
            MapToEntity(model, posting, isCreate: false);
            posting.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to update job posting {PostingId}.", id);
                AddPostingConstraintError(ex);
                return View("Form", await PopulateOptionsAsync(model));
            }

            TempData.SetToast("success", "Success", $"Job posting {posting.ReferenceNumber} updated successfully.");
            return RedirectToAction(nameof(Details), new { id = posting.Id });
        }

        private async Task ValidatePostingAsync(JobPostingFormViewModel model, int? excludeId = null)
        {
            NormalizeForm(model);

            if (!string.IsNullOrWhiteSpace(model.Department) && !HRLists.Departments.Contains(model.Department))
            {
                ModelState.AddModelError(nameof(model.Department), "Select a department from the list.");
            }

            if (!string.IsNullOrWhiteSpace(model.Currency) && !HRLists.Currencies.Contains(model.Currency))
            {
                ModelState.AddModelError(nameof(model.Currency), "Select a currency from the list.");
            }

            if (!string.IsNullOrWhiteSpace(model.ExperienceRequired) && !HRLists.ExperienceLevels.Contains(model.ExperienceRequired))
            {
                ModelState.AddModelError(nameof(model.ExperienceRequired), "Select an experience level from the list.");
            }

            if (!string.IsNullOrWhiteSpace(model.MinimumEducation) && !HRLists.EducationLevels.Contains(model.MinimumEducation))
            {
                ModelState.AddModelError(nameof(model.MinimumEducation), "Select an education level from the list.");
            }

            if (model.HiringManagerUserId.HasValue)
            {
                var managerExists = await _dbContext.Users.AnyAsync(user =>
                    user.Id == model.HiringManagerUserId.Value && user.IsActive);

                if (!managerExists)
                {
                    ModelState.AddModelError(nameof(model.HiringManagerUserId), "Select an active user as the hiring manager.");
                }
            }

            if (await _dbContext.JobPostings.AnyAsync(posting =>
                    posting.JobTitle == model.JobTitle
                    && posting.Department == model.Department
                    && posting.IsActive
                    && (!excludeId.HasValue || posting.Id != excludeId.Value)))
            {
                ModelState.AddModelError(nameof(model.JobTitle),
                    "An active job posting with the same title already exists in this department.");
            }
        }

        private void NormalizeForm(JobPostingFormViewModel model)
        {
            model.JobTitle = TrimRequired(model.JobTitle);
            model.Department = TrimRequired(model.Department);
            model.JobLocation = TrimRequired(model.JobLocation);
            model.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "PKR" : model.Currency.Trim();
            model.ExperienceRequired = TrimRequired(model.ExperienceRequired);
            model.MinimumEducation = TrimRequired(model.MinimumEducation);
            model.JobDescription = TrimRequired(model.JobDescription);
            model.KeyResponsibilities = TrimRequired(model.KeyResponsibilities);
            model.Qualifications = TrimRequired(model.Qualifications);
            model.Benefits = TrimOptional(model.Benefits);
            model.RequiredSkills = (model.RequiredSkills ?? new List<string>())
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Select(skill => skill.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            model.ScreeningQuestions = (model.ScreeningQuestions ?? new List<string>())
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Select(question => question.Trim())
                .ToList();
        }

        private static void MapToEntity(JobPostingFormViewModel model, JobPosting posting, bool isCreate)
        {
            posting.JobTitle = model.JobTitle;
            posting.Department = model.Department;
            posting.EmploymentType = model.EmploymentType;
            posting.WorkplaceType = model.WorkplaceType;
            posting.JobLocation = model.JobLocation;
            posting.Vacancies = model.Vacancies ?? 1;
            posting.MinimumSalary = model.MinimumSalary;
            posting.MaximumSalary = model.MaximumSalary;
            posting.Currency = model.Currency;
            posting.ExperienceRequired = model.ExperienceRequired;
            posting.MinimumEducation = model.MinimumEducation;
            posting.JobDescription = model.JobDescription;
            posting.KeyResponsibilities = model.KeyResponsibilities;
            posting.Qualifications = model.Qualifications;
            posting.ApplicationDeadline = model.ApplicationDeadline
                ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
            posting.Status = model.Status;
            posting.HiringManagerUserId = model.HiringManagerUserId;
            posting.Benefits = model.Benefits;
            if (isCreate)
            {
                posting.IsActive = true;
            }

            foreach (var skill in model.RequiredSkills)
            {
                posting.Skills.Add(new JobPostingSkill { Skill = skill });
            }

            foreach (var question in model.ScreeningQuestions)
            {
                posting.ScreeningQuestions.Add(new JobPostingScreeningQuestion { Question = question });
            }
        }

        private async Task<JobPostingFormViewModel> PopulateOptionsAsync(JobPostingFormViewModel model)
        {
            model.Departments = HRLists.Departments
                .Select(department => new SelectListItem(department, department))
                .ToList();
            model.Currencies = HRLists.Currencies
                .Select(currency => new SelectListItem(currency, currency))
                .ToList();
            model.ExperienceLevels = HRLists.ExperienceLevels
                .Select(level => new SelectListItem(level, level))
                .ToList();
            model.EducationLevels = HRLists.EducationLevels
                .Select(level => new SelectListItem(level, level))
                .ToList();

            var managers = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new
                {
                    user.Id,
                    Name = (user.FirstName + " " + user.LastName).Trim(),
                    user.Role
                })
                .ToListAsync();

            model.HiringManagers = managers
                .Select(user => new SelectListItem(
                    $"{user.Name} ({user.Role.GetDisplayName()})",
                    user.Id.ToString(),
                    user.Id == model.HiringManagerUserId))
                .ToList();

            return model;
        }

        private IActionResult? EnsureCanManage() => CanManage() ? null : RedirectToAction("NotFound", "Error");

        private void AddPostingConstraintError(DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;

            if (detail.Contains("IX_JobPostings_ReferenceNumber", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "A conflicting job reference was generated. Please submit the form again.");
                TempData.SetToast("error", "Please Retry", "A conflicting job reference was generated. Submit the form again.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "The job posting could not be saved due to a data conflict. Please review the details and try again.");
                TempData.SetToast("error", "Save Failed", "The job posting could not be saved. Please try again.");
            }
        }

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private static string TrimRequired(string? value) => value?.Trim() ?? string.Empty;

        private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}