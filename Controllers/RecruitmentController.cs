using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    /// <summary>
    /// Screen 2 — Recruitment Management. A single hub that presents the
    /// hiring pipeline (job openings and applications) with summary cards,
    /// search, filters, sorting, and pagination. Job posting / application
    /// creation and editing live in their existing controllers and are
    /// surfaced from here.
    /// </summary>
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class RecruitmentController : Controller
    {
        private const int DefaultPageSize = 10;
        private static readonly int[] AllowedPageSizes = { 10, 25, 50 };

        private readonly ApplicationDbContext _dbContext;

        public RecruitmentController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? tab = null,
            string? search = null,
            string? department = null,
            EmploymentType? employmentType = null,
            JobPostingStatus? openingStatus = null,
            string? openingSort = null,
            int? jobPostingId = null,
            JobApplicationStatus? applicationStatus = null,
            string? applicationSort = null,
            int? page = null,
            int? pageSize = null)
        {
            var activeTab = string.Equals(tab, "applications", StringComparison.OrdinalIgnoreCase)
                ? "applications"
                : "openings";

            var resolvedPageSize = pageSize.HasValue && AllowedPageSizes.Contains(pageSize.Value)
                ? pageSize.Value
                : DefaultPageSize;

            var model = new RecruitmentIndexViewModel
            {
                ActiveTab = activeTab,
                CanManage = CanManage(),
                PageSize = resolvedPageSize,
                // The search box is shared by both tabs; only the active tab renders it.
                OpeningSearch = activeTab == "openings" ? search : null,
                OpeningDepartment = department,
                OpeningEmploymentType = employmentType,
                OpeningStatus = openingStatus,
                OpeningSortBy = string.IsNullOrWhiteSpace(openingSort) ? "newest" : openingSort,
                ApplicationSearch = activeTab == "applications" ? search : null,
                ApplicationJobPostingId = jobPostingId,
                ApplicationStatus = applicationStatus,
                ApplicationSortBy = string.IsNullOrWhiteSpace(applicationSort) ? "newest" : applicationSort
            };

            await PopulateSummaryAsync(model);

            if (activeTab == "applications")
            {
                await PopulateApplicationsAsync(model, page ?? 1);
            }
            else
            {
                await PopulateOpeningsAsync(model, page ?? 1);
            }

            return View(model);
        }

        private async Task PopulateSummaryAsync(RecruitmentIndexViewModel model)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            model.TotalJobOpenings = await _dbContext.JobPostings.AsNoTracking()
                .CountAsync(posting => posting.IsActive);
            model.OpenPositions = await _dbContext.JobPostings.AsNoTracking()
                .CountAsync(posting => posting.IsActive
                    && posting.Status == JobPostingStatus.Open
                    && posting.ApplicationDeadline >= today);
            model.TotalApplications = await _dbContext.JobApplications.AsNoTracking()
                .CountAsync(application => application.IsActive);
            model.UnderReview = await _dbContext.JobApplications.AsNoTracking()
                .CountAsync(application => application.IsActive && application.Status == JobApplicationStatus.Screening);
            model.ShortlistedCandidates = await _dbContext.JobApplications.AsNoTracking()
                .CountAsync(application => application.IsActive && application.Status == JobApplicationStatus.Shortlisted);
            model.InterviewsScheduled = await _dbContext.Interviews.AsNoTracking()
                .CountAsync(interview => interview.IsActive && interview.Status == InterviewStatus.Scheduled);
        }

        private async Task PopulateOpeningsAsync(RecruitmentIndexViewModel model, int page)
        {
            var query = _dbContext.JobPostings.AsNoTracking().Where(posting => posting.IsActive);

            if (!string.IsNullOrWhiteSpace(model.OpeningSearch))
            {
                var term = model.OpeningSearch.Trim();
                query = query.Where(posting => posting.JobTitle.Contains(term)
                    || posting.ReferenceNumber.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(model.OpeningDepartment))
            {
                query = query.Where(posting => posting.Department == model.OpeningDepartment);
            }

            if (model.OpeningEmploymentType.HasValue)
            {
                query = query.Where(posting => posting.EmploymentType == model.OpeningEmploymentType.Value);
            }

            if (model.OpeningStatus.HasValue)
            {
                query = query.Where(posting => posting.Status == model.OpeningStatus.Value);
            }

            query = model.OpeningSortBy switch
            {
                "oldest" => query.OrderBy(posting => posting.CreatedAt),
                "deadline" => query.OrderBy(posting => posting.ApplicationDeadline),
                "title" => query.OrderBy(posting => posting.JobTitle),
                "vacancies" => query.OrderByDescending(posting => posting.Vacancies),
                _ => query.OrderByDescending(posting => posting.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Openings = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(posting => new RecruitmentOpeningItemViewModel
                {
                    Id = posting.Id,
                    ReferenceNumber = posting.ReferenceNumber,
                    JobTitle = posting.JobTitle,
                    Department = posting.Department,
                    EmploymentType = posting.EmploymentType,
                    WorkplaceType = posting.WorkplaceType,
                    Vacancies = posting.Vacancies,
                    ApplicationCount = posting.Applications.Count(application => application.IsActive),
                    ApplicationDeadline = posting.ApplicationDeadline,
                    Status = posting.Status
                })
                .ToListAsync();

            model.Departments = await _dbContext.JobPostings.AsNoTracking()
                .Where(posting => posting.IsActive)
                .Select(posting => posting.Department)
                .Distinct()
                .OrderBy(department => department)
                .ToListAsync();
        }

        private async Task PopulateApplicationsAsync(RecruitmentIndexViewModel model, int page)
        {
            var query = _dbContext.JobApplications.AsNoTracking().Where(application => application.IsActive);

            if (!string.IsNullOrWhiteSpace(model.ApplicationSearch))
            {
                var term = model.ApplicationSearch.Trim();
                query = query.Where(application => application.ApplicationNumber.Contains(term)
                    || application.FirstName.Contains(term)
                    || application.LastName.Contains(term)
                    || application.Email.Contains(term)
                    || (application.FirstName + " " + application.LastName).Contains(term));
            }

            if (model.ApplicationJobPostingId.HasValue)
            {
                query = query.Where(application => application.JobPostingId == model.ApplicationJobPostingId.Value);
            }

            if (model.ApplicationStatus.HasValue)
            {
                query = query.Where(application => application.Status == model.ApplicationStatus.Value);
            }

            query = model.ApplicationSortBy switch
            {
                "oldest" => query.OrderBy(application => application.CreatedAt),
                "name" => query.OrderBy(application => application.FirstName).ThenBy(application => application.LastName),
                "status" => query.OrderBy(application => application.Status).ThenByDescending(application => application.CreatedAt),
                "position" => query.OrderBy(application => application.JobPosting!.JobTitle).ThenByDescending(application => application.CreatedAt),
                _ => query.OrderByDescending(application => application.CreatedAt)
            };

            model.TotalRecords = await query.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize));
            model.CurrentPage = Math.Clamp(page, 1, model.TotalPages);

            model.Applications = await query
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(application => new RecruitmentApplicationItemViewModel
                {
                    Id = application.Id,
                    ApplicationNumber = application.ApplicationNumber,
                    CandidateName = (application.FirstName + " " + application.LastName).Trim(),
                    Email = application.Email,
                    Phone = application.Phone,
                    JobPostingId = application.JobPostingId,
                    JobTitle = application.JobPosting != null ? application.JobPosting.JobTitle : string.Empty,
                    JobReference = application.JobPosting != null ? application.JobPosting.ReferenceNumber : string.Empty,
                    ApplicationDate = application.ApplicationDate,
                    Status = application.Status,
                    InterviewsScheduled = application.Interviews.Count(interview =>
                        interview.IsActive && interview.Status == InterviewStatus.Scheduled)
                })
                .ToListAsync();

            foreach (var item in model.Applications)
            {
                item.Initials = Initials(item.CandidateName);
            }

            var postingOptions = await _dbContext.JobPostings.AsNoTracking()
                .Where(posting => posting.IsActive)
                .OrderByDescending(posting => posting.CreatedAt)
                .Select(posting => new { posting.Id, posting.JobTitle, posting.ReferenceNumber })
                .ToListAsync();

            model.JobPostings = postingOptions
                .Select(posting => new SelectListItem(
                    $"{posting.JobTitle} ({posting.ReferenceNumber})",
                    posting.Id.ToString(),
                    posting.Id == model.ApplicationJobPostingId))
                .ToList();
        }

        private bool CanManage() =>
            RolePermissions.TryGetCurrentRole(User, out var role)
            && role is UserRole.SuperAdmin or UserRole.Admin or UserRole.StaffManager;

        private static string Initials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0
                ? "NA"
                : string.Concat(parts.Take(2).Select(part => part[0])).ToUpperInvariant();
        }
    }
}
