using System.Security.Claims;
using System.Text;
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
    /// Screen 7 — HR Reports. Read-only analytics across the completed HR
    /// screens (Employees, Recruitment, Offers, Performance and Termination).
    ///
    /// Every figure is computed from live database records using grouped and
    /// aggregated queries — nothing is hardcoded or fabricated. The calculations
    /// mirror the statuses and workflows of the source screens, and one filter +
    /// export pipeline is shared by all four detail reports.
    /// </summary>
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class HRReportsController : Controller
    {
        private static readonly int[] PageSizes = { 10, 25, 50 };

        /// <summary>Chart palette shared with the commercial Reports module.</summary>
        private static readonly string[] ChartColors =
        {
            "#d6a652", "#171614", "#4f8b6d", "#c77b4f", "#7b8494", "#ead09a", "#8e6640", "#9d4f4f"
        };

        private readonly ApplicationDbContext _dbContext;

        public HRReportsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ==================================================================
        // Dashboard
        // ==================================================================

        [HttpGet]
        public async Task<IActionResult> Index() => View(await BuildDashboardAsync());

        private async Task<HRReportDashboardViewModel> BuildDashboardAsync()
        {
            var employees = _dbContext.Employees.AsNoTracking().Where(employee => employee.IsActive);
            var totalEmployees = await employees.CountAsync();
            var activeEmployees = await employees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.Active);
            var inactiveEmployees = await employees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.Inactive);
            var onLeaveEmployees = await employees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.OnLeave);

            var postings = _dbContext.JobPostings.AsNoTracking().Where(posting => posting.IsActive);
            var totalOpenings = await postings.CountAsync();
            var openOpenings = await postings.CountAsync(posting => posting.Status == JobPostingStatus.Open);
            var totalApplications = await _dbContext.JobApplications.AsNoTracking().CountAsync(application => application.IsActive);

            var offers = _dbContext.JobOffers.AsNoTracking().Where(offer => offer.IsActive);
            var pendingOffers = await offers.CountAsync(offer =>
                offer.Status == JobOfferStatus.Draft || offer.Status == JobOfferStatus.PendingAcceptance);
            var acceptedOffers = await offers.CountAsync(offer =>
                offer.Status == JobOfferStatus.Accepted || offer.Status == JobOfferStatus.Hired);

            var cases = _dbContext.PerformanceCases.AsNoTracking().Where(performanceCase => performanceCase.IsActive);
            var openCases = await cases.CountAsync(performanceCase =>
                performanceCase.Status == PerformanceCaseStatus.Open
                || performanceCase.Status == PerformanceCaseStatus.UnderReview
                || performanceCase.Status == PerformanceCaseStatus.ImprovementPlan
                || performanceCase.Status == PerformanceCaseStatus.Warning);
            var activeWarnings = await _dbContext.PerformanceWarnings.AsNoTracking()
                .CountAsync(warning => warning.IsActive && warning.Status != WarningStatus.Closed);

            var terminations = _dbContext.TerminationRequests.AsNoTracking().Where(request => request.IsActive);
            var pendingTerminations = await terminations.CountAsync(request =>
                request.Status != TerminationStatus.Completed && request.Status != TerminationStatus.Rejected);
            var completedTerminations = await terminations.CountAsync(request => request.Status == TerminationStatus.Completed);

            var model = new HRReportDashboardViewModel
            {
                RefreshedAt = DateTime.UtcNow,
                Kpis =
                [
                    Kpi("Total Employees", totalEmployees.ToString("N0"), "Active HR records", "bi-people"),
                    Kpi("Active Employees", activeEmployees.ToString("N0"), "Currently employed", "bi-person-check", "positive"),
                    Kpi("Inactive Employees", inactiveEmployees.ToString("N0"), "Offboarded staff", "bi-person-dash", inactiveEmployees > 0 ? "warning" : string.Empty),
                    Kpi("Job Openings", totalOpenings.ToString("N0"), $"{openOpenings:N0} currently open", "bi-briefcase"),
                    Kpi("Applications", totalApplications.ToString("N0"), "Applications received", "bi-file-earmark-text"),
                    Kpi("Pending Offers", pendingOffers.ToString("N0"), "Draft or awaiting response", "bi-hourglass-split", pendingOffers > 0 ? "warning" : string.Empty),
                    Kpi("Accepted Offers", acceptedOffers.ToString("N0"), "Accepted or hired", "bi-patch-check", "positive"),
                    Kpi("Open Performance Cases", openCases.ToString("N0"), "Cases still in progress", "bi-exclamation-diamond", openCases > 0 ? "warning" : string.Empty),
                    Kpi("Active Warnings", activeWarnings.ToString("N0"), "Warnings not closed", "bi-shield-exclamation"),
                    Kpi("Pending Terminations", pendingTerminations.ToString("N0"), "Not completed or rejected", "bi-box-arrow-right"),
                    Kpi("Completed Terminations", completedTerminations.ToString("N0"), "Offboarding finished", "bi-flag", "positive")
                ]
            };

            var applicationsByStatus = await _dbContext.JobApplications.AsNoTracking()
                .Where(application => application.IsActive)
                .GroupBy(application => application.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() })
                .ToListAsync();
            var offersByStatus = await offers
                .GroupBy(offer => offer.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() })
                .ToListAsync();
            var employeesByStatus = await employees
                .GroupBy(employee => employee.EmploymentStatus)
                .Select(group => new { Status = group.Key, Value = group.Count() })
                .ToListAsync();
            var employeesByDepartment = await employees
                .GroupBy(employee => employee.Department)
                .Select(group => new { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value)
                .Take(8)
                .ToListAsync();
            var casesByStatus = await cases
                .GroupBy(performanceCase => performanceCase.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() })
                .ToListAsync();
            var terminationsByStatus = await terminations
                .GroupBy(request => request.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() })
                .ToListAsync();

            model.Charts =
            [
                Chart("hrDashApplications", "Applications by Status", "Recruitment pipeline distribution", "doughnut",
                    applicationsByStatus.Select(item => item.Status.GetDisplayName()),
                    applicationsByStatus.Select(item => (decimal)item.Value)),
                Chart("hrDashOffers", "Offers by Status", "Offer and hiring outcomes", "doughnut",
                    offersByStatus.Select(item => item.Status.GetDisplayName()),
                    offersByStatus.Select(item => (decimal)item.Value)),
                Chart("hrDashDepartments", "Employees by Department", "Workforce distribution", "bar",
                    employeesByDepartment.Select(item => item.Name),
                    employeesByDepartment.Select(item => (decimal)item.Value)),
                Chart("hrDashEmployeeStatus", "Employment Status", "Active, on leave and inactive staff", "doughnut",
                    employeesByStatus.Select(item => item.Status.GetDisplayName()),
                    employeesByStatus.Select(item => (decimal)item.Value)),
                Chart("hrDashCases", "Performance Cases by Status", "People-management workload", "bar",
                    casesByStatus.Select(item => item.Status.GetDisplayName()),
                    casesByStatus.Select(item => (decimal)item.Value)),
                Chart("hrDashTerminations", "Termination Requests by Status", "Offboarding workload", "doughnut",
                    terminationsByStatus.Select(item => item.Status.GetDisplayName()),
                    terminationsByStatus.Select(item => (decimal)item.Value))
            ];

            var largestDepartment = employeesByDepartment.FirstOrDefault();
            model.Insights =
            [
                Insight("Largest department", largestDepartment?.Name ?? "No data",
                    largestDepartment == null ? "No employees recorded" : $"{largestDepartment.Value:N0} employees", "bi-diagram-3"),
                Insight("Offer conversion", Percent(acceptedOffers, totalApplications),
                    "Accepted offers against all applications", "bi-funnel", "positive"),
                Insight("Recruitment attention", $"{pendingOffers:N0} pending offers",
                    pendingOffers > 0 ? "Follow up on offers awaiting a response" : "No offers awaiting a response",
                    "bi-hourglass-split", pendingOffers > 0 ? "warning" : string.Empty),
                Insight("Clearance backlog", $"{pendingTerminations:N0} open terminations",
                    pendingTerminations > 0 ? "Offboarding requests still in flight" : "No open termination requests",
                    "bi-clipboard-check", pendingTerminations > 0 ? "risk" : "positive"),
                Insight("Workforce note", $"{onLeaveEmployees:N0} on leave",
                    $"{activeEmployees:N0} active against {inactiveEmployees:N0} inactive", "bi-people")
            ];

            model.Modules =
            [
                Module("Recruitment Report", "Openings, pipeline and hiring outcomes", nameof(Recruitment), "bi-person-plus"),
                Module("Employee Report", "Workforce distribution and joiners", nameof(Employees), "bi-people"),
                Module("Performance Report", "Cases, warnings and improvement plans", nameof(Performance), "bi-graph-up-arrow"),
                Module("Termination Report", "Offboarding and clearance progress", nameof(Termination), "bi-box-arrow-right")
            ];

            return model;
        }

        // ==================================================================
        // Recruitment report
        // ==================================================================

        [HttpGet]
        public async Task<IActionResult> Recruitment([FromQuery] HRReportFilterViewModel filter)
            => View(await BuildRecruitmentAsync(filter));

        private async Task<HRReportPageViewModel> BuildRecruitmentAsync(HRReportFilterViewModel filter, bool export = false)
        {
            var model = BasePage("recruitment", nameof(Recruitment), filter, export);
            model.Title = "Recruitment Report";
            model.Description = "Job openings by status and department, application pipeline, interviews and offer outcomes.";
            model.Icon = "bi-person-plus";
            model.StatusLabel = "Opening Status";
            model.CategoryLabel = "Employment Type";
            model.StatusOptions = Enum.GetValues<JobPostingStatus>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();
            model.CategoryOptions = Enum.GetValues<EmploymentType>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();

            var from = model.RangeStart.ToDateTime(TimeOnly.MinValue);
            var until = model.RangeEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var postings = _dbContext.JobPostings.AsNoTracking()
                .Where(posting => posting.IsActive && posting.CreatedAt >= from && posting.CreatedAt < until);

            if (!string.IsNullOrWhiteSpace(model.Filter.Department))
            {
                var department = model.Filter.Department;
                postings = postings.Where(posting => posting.Department == department);
            }

            if (Enum.TryParse<JobPostingStatus>(model.Filter.Status, true, out var postingStatus))
            {
                postings = postings.Where(posting => posting.Status == postingStatus);
            }

            if (Enum.TryParse<EmploymentType>(model.Filter.Category, true, out var employmentType))
            {
                postings = postings.Where(posting => posting.EmploymentType == employmentType);
            }

            if (!string.IsNullOrWhiteSpace(model.Filter.Search))
            {
                var term = model.Filter.Search;
                postings = postings.Where(posting =>
                    posting.JobTitle.Contains(term)
                    || posting.ReferenceNumber.Contains(term)
                    || posting.Department.Contains(term)
                    || posting.JobLocation.Contains(term));
            }

            var postingIds = postings.Select(posting => posting.Id);
            var applications = _dbContext.JobApplications.AsNoTracking()
                .Where(application => application.IsActive && postingIds.Contains(application.JobPostingId));

            var totalOpenings = await postings.CountAsync();
            var openOpenings = await postings.CountAsync(posting => posting.Status == JobPostingStatus.Open);
            var closedOpenings = await postings.CountAsync(posting => posting.Status == JobPostingStatus.Closed);
            var vacancies = await postings.SumAsync(posting => (int?)posting.Vacancies) ?? 0;
            var totalApplications = await applications.CountAsync();
            var shortlisted = await applications.CountAsync(application => application.Status == JobApplicationStatus.Shortlisted);
            var hired = await applications.CountAsync(application => application.Status == JobApplicationStatus.Hired);
            var interviews = await _dbContext.Interviews.AsNoTracking()
                .CountAsync(interview => interview.IsActive && postingIds.Contains(interview.JobApplication!.JobPostingId));
            var scheduledInterviews = await _dbContext.Interviews.AsNoTracking()
                .CountAsync(interview => interview.IsActive && interview.Status == InterviewStatus.Scheduled
                    && postingIds.Contains(interview.JobApplication!.JobPostingId));

            var offersByStatus = await (from offer in _dbContext.JobOffers.AsNoTracking()
                                        join application in _dbContext.JobApplications.AsNoTracking() on offer.JobApplicationId equals application.Id
                                        where offer.IsActive && postingIds.Contains(application.JobPostingId)
                                        group offer by offer.Status into grouped
                                        select new { Status = grouped.Key, Value = grouped.Count() }).ToListAsync();
            var offersIssued = offersByStatus.Sum(item => item.Value);
            var offersAccepted = offersByStatus
                .Where(item => item.Status == JobOfferStatus.Accepted || item.Status == JobOfferStatus.Hired)
                .Sum(item => item.Value);

            model.Kpis =
            [
                Kpi("Job Openings", totalOpenings.ToString("N0"), "Postings in scope", "bi-briefcase"),
                Kpi("Open Openings", openOpenings.ToString("N0"), "Currently accepting", "bi-unlock", "positive"),
                Kpi("Closed Openings", closedOpenings.ToString("N0"), "No longer active", "bi-lock", closedOpenings > 0 ? "warning" : string.Empty),
                Kpi("Total Vacancies", vacancies.ToString("N0"), "Positions advertised", "bi-people"),
                Kpi("Applications", totalApplications.ToString("N0"), "Applications received", "bi-file-earmark-text"),
                Kpi("Shortlisted", shortlisted.ToString("N0"), "Reached shortlist", "bi-person-check"),
                Kpi("Interviews", interviews.ToString("N0"), $"{scheduledInterviews:N0} still scheduled", "bi-calendar2-event"),
                Kpi("Offers Issued", offersIssued.ToString("N0"), "Offers sent to candidates", "bi-envelope-paper"),
                Kpi("Offers Accepted", offersAccepted.ToString("N0"), "Accepted or hired", "bi-patch-check", "positive"),
                Kpi("Hire Rate", Percent(hired, totalApplications), "Hired against applications", "bi-graph-up")
            ];

            var openingsByStatus = await postings.GroupBy(posting => posting.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var openingsByDepartment = await postings.GroupBy(posting => posting.Department)
                .Select(group => new { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value).Take(8).ToListAsync();
            var applicationsByStatus = await applications.GroupBy(application => application.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();

            model.Charts =
            [
                Chart("hrRecStatus", "Openings by Status", "Posting lifecycle distribution", "doughnut",
                    openingsByStatus.Select(item => item.Status.GetDisplayName()), openingsByStatus.Select(item => (decimal)item.Value)),
                Chart("hrRecDepartment", "Openings by Department", "Where hiring is focused", "bar",
                    openingsByDepartment.Select(item => item.Name), openingsByDepartment.Select(item => (decimal)item.Value)),
                Chart("hrRecApplications", "Applications by Status", "Candidate pipeline distribution", "doughnut",
                    applicationsByStatus.Select(item => item.Status.GetDisplayName()), applicationsByStatus.Select(item => (decimal)item.Value)),
                Chart("hrRecOffers", "Offers by Status", "Offer outcome distribution", "doughnut",
                    offersByStatus.Select(item => item.Status.GetDisplayName()), offersByStatus.Select(item => (decimal)item.Value))
            ];

            model.TotalRecords = totalOpenings;
            CountPages(model);
            model.Columns = ["Reference", "Job Title", "Department", "Type", "Status", "Vacancies",
                "Applications", "Shortlisted", "Interviews", "Offers", "Hired", "Posted"];

            var orderedPostings = model.Filter.SortBy switch
            {
                "oldest" => postings.OrderBy(posting => posting.CreatedAt),
                "name" or "title" => postings.OrderBy(posting => posting.JobTitle),
                "department" => postings.OrderBy(posting => posting.Department).ThenBy(posting => posting.JobTitle),
                _ => postings.OrderByDescending(posting => posting.CreatedAt)
            };

            var page = await orderedPostings
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(posting => new
                {
                    posting.Id,
                    posting.ReferenceNumber,
                    posting.JobTitle,
                    posting.Department,
                    posting.EmploymentType,
                    posting.Status,
                    posting.Vacancies,
                    posting.CreatedAt
                })
                .ToListAsync();

            var pageIds = page.Select(item => item.Id).ToList();
            var applicationStats = await _dbContext.JobApplications.AsNoTracking()
                .Where(application => application.IsActive && pageIds.Contains(application.JobPostingId))
                .GroupBy(application => application.JobPostingId)
                .Select(group => new
                {
                    Id = group.Key,
                    Applications = group.Count(),
                    Shortlisted = group.Count(application => application.Status == JobApplicationStatus.Shortlisted),
                    Hired = group.Count(application => application.Status == JobApplicationStatus.Hired)
                })
                .ToListAsync();
            var interviewStats = await (from interview in _dbContext.Interviews.AsNoTracking()
                                        join application in _dbContext.JobApplications.AsNoTracking() on interview.JobApplicationId equals application.Id
                                        where interview.IsActive && pageIds.Contains(application.JobPostingId)
                                        group interview by application.JobPostingId into grouped
                                        select new { Id = grouped.Key, Value = grouped.Count() }).ToListAsync();
            var offerStats = await (from offer in _dbContext.JobOffers.AsNoTracking()
                                    join application in _dbContext.JobApplications.AsNoTracking() on offer.JobApplicationId equals application.Id
                                    where offer.IsActive && pageIds.Contains(application.JobPostingId)
                                    group offer by application.JobPostingId into grouped
                                    select new { Id = grouped.Key, Value = grouped.Count() }).ToListAsync();

            var applicationLookup = applicationStats.ToDictionary(item => item.Id);
            var interviewLookup = interviewStats.ToDictionary(item => item.Id, item => item.Value);
            var offerLookup = offerStats.ToDictionary(item => item.Id, item => item.Value);

            model.Rows = page.Select(item =>
            {
                var stats = applicationLookup.TryGetValue(item.Id, out var found) ? found : null;
                var interviewCount = interviewLookup.TryGetValue(item.Id, out var interviewed) ? interviewed : 0;
                var offerCount = offerLookup.TryGetValue(item.Id, out var offered) ? offered : 0;
                return Row(
                [
                    item.ReferenceNumber,
                    item.JobTitle,
                    item.Department,
                    item.EmploymentType.GetDisplayName(),
                    item.Status.GetDisplayName(),
                    item.Vacancies.ToString("N0"),
                    (stats?.Applications ?? 0).ToString("N0"),
                    (stats?.Shortlisted ?? 0).ToString("N0"),
                    interviewCount.ToString("N0"),
                    offerCount.ToString("N0"),
                    (stats?.Hired ?? 0).ToString("N0"),
                    item.CreatedAt.ToString("dd MMM yyyy")
                ], item.Status.GetDisplayName(), item.Status.GetBadgeClass(), Url.Action("Details", "JobPostings", new { id = item.Id }));
            }).ToList();

            var topDepartment = openingsByDepartment.FirstOrDefault();
            var pendingOffers = offersByStatus
                .Where(item => item.Status == JobOfferStatus.Draft || item.Status == JobOfferStatus.PendingAcceptance)
                .Sum(item => item.Value);

            model.Insights =
            [
                Insight("Largest opening", topDepartment?.Name ?? "No data",
                    topDepartment == null ? "No openings in scope" : $"{topDepartment.Value:N0} openings", "bi-briefcase"),
                Insight("Offer conversion", Percent(offersAccepted, offersIssued),
                    "Accepted offers against offers issued", "bi-funnel", "positive"),
                Insight("Recruitment attention", $"{pendingOffers:N0} pending offers",
                    pendingOffers > 0 ? "Draft or awaiting a candidate response" : "No offers awaiting a response",
                    "bi-hourglass-split", pendingOffers > 0 ? "warning" : string.Empty)
            ];

            return model;
        }

        // ==================================================================
        // Employees report
        // ==================================================================

        [HttpGet]
        public async Task<IActionResult> Employees([FromQuery] HRReportFilterViewModel filter)
            => View(await BuildEmployeesAsync(filter));

        private async Task<HRReportPageViewModel> BuildEmployeesAsync(HRReportFilterViewModel filter, bool export = false)
        {
            var model = BasePage("employees", nameof(Employees), filter, export);
            model.Title = "Employee Report";
            model.Description = "Workforce distribution by department, designation, employment type and status.";
            model.Icon = "bi-people";
            model.StatusLabel = "Employment Status";
            model.CategoryLabel = "Employment Type";
            model.StatusOptions = Enum.GetValues<EmploymentStatus>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();
            model.CategoryOptions = Enum.GetValues<EmploymentType>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();

            var employees = _dbContext.Employees.AsNoTracking()
                .Where(employee => employee.IsActive
                    && employee.JoiningDate >= model.RangeStart
                    && employee.JoiningDate <= model.RangeEnd);

            if (!string.IsNullOrWhiteSpace(model.Filter.Department))
            {
                var department = model.Filter.Department;
                employees = employees.Where(employee => employee.Department == department);
            }

            if (Enum.TryParse<EmploymentStatus>(model.Filter.Status, true, out var status))
            {
                employees = employees.Where(employee => employee.EmploymentStatus == status);
            }

            if (Enum.TryParse<EmploymentType>(model.Filter.Category, true, out var employmentType))
            {
                employees = employees.Where(employee => employee.EmploymentType == employmentType);
            }

            if (!string.IsNullOrWhiteSpace(model.Filter.Search))
            {
                var term = model.Filter.Search;
                employees = employees.Where(employee =>
                    employee.FirstName.Contains(term)
                    || employee.LastName.Contains(term)
                    || employee.EmployeeNumber.Contains(term)
                    || employee.Designation.Contains(term));
            }

            var total = await employees.CountAsync();
            var active = await employees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.Active);
            var onLeave = await employees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.OnLeave);
            var inactive = await employees.CountAsync(employee => employee.EmploymentStatus == EmploymentStatus.Inactive);
            var departmentCount = await employees.Select(employee => employee.Department).Distinct().CountAsync();
            var designationCount = await employees.Select(employee => employee.Designation).Distinct().CountAsync();

            model.Kpis =
            [
                Kpi("Total Employees", total.ToString("N0"), "Employees in scope", "bi-people"),
                Kpi("Active", active.ToString("N0"), "Currently employed", "bi-person-check", "positive"),
                Kpi("On Leave", onLeave.ToString("N0"), "Temporarily away", "bi-person-dash"),
                Kpi("Inactive", inactive.ToString("N0"), "Offboarded", "bi-person-x", inactive > 0 ? "warning" : string.Empty),
                Kpi("Departments", departmentCount.ToString("N0"), "Distinct departments", "bi-diagram-3"),
                Kpi("Designations", designationCount.ToString("N0"), "Distinct roles", "bi-person-badge")
            ];

            var byDepartment = await employees.GroupBy(employee => employee.Department)
                .Select(group => new { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value).Take(10).ToListAsync();
            var byDesignation = await employees.GroupBy(employee => employee.Designation)
                .Select(group => new { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value).Take(8).ToListAsync();
            var byType = await employees.GroupBy(employee => employee.EmploymentType)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var byStatus = await employees.GroupBy(employee => employee.EmploymentStatus)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();

            var joiningDates = await employees.Select(employee => employee.JoiningDate).ToListAsync();
            var byMonth = joiningDates
                .GroupBy(date => new DateOnly(date.Year, date.Month, 1))
                .OrderBy(group => group.Key)
                .Select(group => new { Label = group.Key.ToString("MMM yyyy"), Value = group.Count() })
                .ToList();

            model.Charts =
            [
                Chart("hrEmpDepartment", "Employees by Department", "Workforce distribution", "bar",
                    byDepartment.Select(item => item.Name), byDepartment.Select(item => (decimal)item.Value)),
                Chart("hrEmpDesignation", "Employees by Designation", "Role distribution", "bar",
                    byDesignation.Select(item => item.Name), byDesignation.Select(item => (decimal)item.Value)),
                Chart("hrEmpType", "Employment Type", "Full-time, part-time and contract mix", "doughnut",
                    byType.Select(item => item.Status.GetDisplayName()), byType.Select(item => (decimal)item.Value)),
                Chart("hrEmpStatus", "Employment Status", "Active, on leave and inactive staff", "doughnut",
                    byStatus.Select(item => item.Status.GetDisplayName()), byStatus.Select(item => (decimal)item.Value)),
                Chart("hrEmpJoiners", "Joiners by Month", "Hiring trend across the selected range", "line",
                    byMonth.Select(item => item.Label), byMonth.Select(item => (decimal)item.Value))
            ];

            model.TotalRecords = total;
            CountPages(model);
            model.Columns = ["Employee", "Number", "Department", "Designation", "Type", "Status", "Joined", "Tenure"];

            var orderedEmployees = model.Filter.SortBy switch
            {
                "oldest" => employees.OrderBy(employee => employee.JoiningDate),
                "name" => employees.OrderBy(employee => employee.FirstName).ThenBy(employee => employee.LastName),
                "department" => employees.OrderBy(employee => employee.Department).ThenBy(employee => employee.FirstName),
                _ => employees.OrderByDescending(employee => employee.JoiningDate).ThenBy(employee => employee.FirstName)
            };

            var page = await orderedEmployees
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(employee => new
                {
                    employee.Id,
                    employee.FirstName,
                    employee.LastName,
                    employee.EmployeeNumber,
                    employee.Department,
                    employee.Designation,
                    employee.EmploymentType,
                    employee.EmploymentStatus,
                    employee.JoiningDate
                })
                .ToListAsync();

            model.Rows = page.Select(item => Row(
            [
                $"{item.FirstName} {item.LastName}".Trim(),
                item.EmployeeNumber,
                item.Department,
                item.Designation,
                item.EmploymentType.GetDisplayName(),
                item.EmploymentStatus.GetDisplayName(),
                item.JoiningDate.ToString("dd MMM yyyy"),
                Tenure(item.JoiningDate)
            ], item.EmploymentStatus.GetDisplayName(), item.EmploymentStatus.GetBadgeClass(), Url.Action("Details", "Employees", new { id = item.Id }))).ToList();

            var topDepartment = byDepartment.FirstOrDefault();
            var topDesignation = byDesignation.FirstOrDefault();
            var latestJoiners = byMonth.LastOrDefault();

            model.Insights =
            [
                Insight("Largest department", topDepartment?.Name ?? "No data",
                    topDepartment == null ? "No employees in scope" : $"{topDepartment.Value:N0} employees", "bi-diagram-3"),
                Insight("Most common role", topDesignation?.Name ?? "No data",
                    topDesignation == null ? "No roles recorded" : $"{topDesignation.Value:N0} employees", "bi-person-badge"),
                Insight("Newest joiners", latestJoiners?.Label ?? "No data",
                    latestJoiners == null ? "No joiners in scope" : $"{latestJoiners.Value:N0} joined in the latest month", "bi-calendar-plus")
            ];

            return model;
        }

        // ==================================================================
        // Performance report
        // ==================================================================

        [HttpGet]
        public async Task<IActionResult> Performance([FromQuery] HRReportFilterViewModel filter)
            => View(await BuildPerformanceAsync(filter));

        private async Task<HRReportPageViewModel> BuildPerformanceAsync(HRReportFilterViewModel filter, bool export = false)
        {
            var model = BasePage("performance", nameof(Performance), filter, export);
            model.Title = "Performance Report";
            model.Description = "Performance and conduct cases, warnings and improvement-plan progress.";
            model.Icon = "bi-graph-up-arrow";
            model.StatusLabel = "Case Status";
            model.CategoryLabel = "Issue Type";
            model.StatusOptions = Enum.GetValues<PerformanceCaseStatus>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();
            model.CategoryOptions = Enum.GetValues<PerformanceIssueType>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();

            var cases = _dbContext.PerformanceCases.AsNoTracking()
                .Where(performanceCase => performanceCase.IsActive
                    && performanceCase.ReportedDate >= model.RangeStart
                    && performanceCase.ReportedDate <= model.RangeEnd);

            if (!string.IsNullOrWhiteSpace(model.Filter.Department))
            {
                var department = model.Filter.Department;
                cases = cases.Where(performanceCase => performanceCase.Employee!.Department == department);
            }

            if (Enum.TryParse<PerformanceCaseStatus>(model.Filter.Status, true, out var caseStatus))
            {
                cases = cases.Where(performanceCase => performanceCase.Status == caseStatus);
            }

            if (Enum.TryParse<PerformanceIssueType>(model.Filter.Category, true, out var issueType))
            {
                cases = cases.Where(performanceCase => performanceCase.IssueType == issueType);
            }

            if (!string.IsNullOrWhiteSpace(model.Filter.Search))
            {
                var term = model.Filter.Search;
                cases = cases.Where(performanceCase =>
                    performanceCase.CaseNumber.Contains(term)
                    || performanceCase.Title.Contains(term)
                    || performanceCase.Employee!.FirstName.Contains(term)
                    || performanceCase.Employee!.LastName.Contains(term));
            }

            var caseIds = cases.Select(performanceCase => performanceCase.Id);
            var totalCases = await cases.CountAsync();
            var openCases = await cases.CountAsync(performanceCase =>
                performanceCase.Status == PerformanceCaseStatus.Open
                || performanceCase.Status == PerformanceCaseStatus.UnderReview
                || performanceCase.Status == PerformanceCaseStatus.ImprovementPlan
                || performanceCase.Status == PerformanceCaseStatus.Warning);
            var resolvedCases = await cases.CountAsync(performanceCase => performanceCase.Status == PerformanceCaseStatus.Resolved);
            var closedCases = await cases.CountAsync(performanceCase => performanceCase.Status == PerformanceCaseStatus.Closed);

            var warnings = await _dbContext.PerformanceWarnings.AsNoTracking()
                .CountAsync(warning => warning.IsActive && caseIds.Contains(warning.PerformanceCaseId));
            var activeWarnings = await _dbContext.PerformanceWarnings.AsNoTracking()
                .CountAsync(warning => warning.IsActive && warning.Status != WarningStatus.Closed && caseIds.Contains(warning.PerformanceCaseId));
            var plans = await _dbContext.ImprovementPlans.AsNoTracking()
                .CountAsync(plan => plan.IsActive && caseIds.Contains(plan.PerformanceCaseId));
            var activePlans = await _dbContext.ImprovementPlans.AsNoTracking()
                .CountAsync(plan => plan.IsActive && (plan.Status == ImprovementPlanStatus.Active || plan.Status == ImprovementPlanStatus.UnderReview)
                    && caseIds.Contains(plan.PerformanceCaseId));
            var overduePlans = await _dbContext.ImprovementPlans.AsNoTracking()
                .CountAsync(plan => plan.IsActive && plan.Status == ImprovementPlanStatus.Overdue && caseIds.Contains(plan.PerformanceCaseId));
            var meetings = await _dbContext.PerformanceMeetings.AsNoTracking()
                .CountAsync(meeting => meeting.IsActive && caseIds.Contains(meeting.PerformanceCaseId));
            var followUps = await _dbContext.PerformanceMeetings.AsNoTracking()
                .CountAsync(meeting => meeting.IsActive && meeting.FollowUpDate != null && caseIds.Contains(meeting.PerformanceCaseId));

            model.Kpis =
            [
                Kpi("Total Cases", totalCases.ToString("N0"), "Cases in scope", "bi-folder2-open"),
                Kpi("Open Cases", openCases.ToString("N0"), "Still being handled", "bi-hourglass-split", openCases > 0 ? "warning" : string.Empty),
                Kpi("Resolved", resolvedCases.ToString("N0"), "Reached resolution", "bi-check2-circle", "positive"),
                Kpi("Closed", closedCases.ToString("N0"), "Fully closed", "bi-archive"),
                Kpi("Warnings", warnings.ToString("N0"), $"{activeWarnings:N0} active", "bi-shield-exclamation"),
                Kpi("Improvement Plans", plans.ToString("N0"), $"{activePlans:N0} active", "bi-clipboard2-pulse"),
                Kpi("Overdue Plans", overduePlans.ToString("N0"), "Past their end date", "bi-alarm", overduePlans > 0 ? "risk" : string.Empty),
                Kpi("Meetings", meetings.ToString("N0"), "Meetings recorded", "bi-people")
            ];

            var byStatus = await cases.GroupBy(performanceCase => performanceCase.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var bySeverity = await cases.GroupBy(performanceCase => performanceCase.Severity)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var byIssue = await cases.GroupBy(performanceCase => performanceCase.IssueType)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var warningsByStatus = await _dbContext.PerformanceWarnings.AsNoTracking()
                .Where(warning => warning.IsActive && caseIds.Contains(warning.PerformanceCaseId))
                .GroupBy(warning => warning.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var plansByStatus = await _dbContext.ImprovementPlans.AsNoTracking()
                .Where(plan => plan.IsActive && caseIds.Contains(plan.PerformanceCaseId))
                .GroupBy(plan => plan.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();

            model.Charts =
            [
                Chart("hrPerfStatus", "Cases by Status", "Case lifecycle distribution", "doughnut",
                    byStatus.Select(item => item.Status.GetDisplayName()), byStatus.Select(item => (decimal)item.Value)),
                Chart("hrPerfSeverity", "Cases by Severity", "How serious cases are", "bar",
                    bySeverity.Select(item => item.Status.GetDisplayName()), bySeverity.Select(item => (decimal)item.Value)),
                Chart("hrPerfIssue", "Cases by Issue Type", "Performance, conduct, attendance and compliance", "doughnut",
                    byIssue.Select(item => item.Status.GetDisplayName()), byIssue.Select(item => (decimal)item.Value)),
                Chart("hrPerfWarnings", "Warnings by Status", "Warning follow-through", "doughnut",
                    warningsByStatus.Select(item => item.Status.GetDisplayName()), warningsByStatus.Select(item => (decimal)item.Value)),
                Chart("hrPerfPlans", "Improvement Plans by Status", "Plan progress", "doughnut",
                    plansByStatus.Select(item => item.Status.GetDisplayName()), plansByStatus.Select(item => (decimal)item.Value))
            ];

            model.TotalRecords = totalCases;
            CountPages(model);
            model.Columns = ["Case", "Employee", "Department", "Issue Type", "Severity", "Status", "Reported", "Meetings", "Warnings", "Plans"];

            var orderedCases = model.Filter.SortBy switch
            {
                "oldest" => cases.OrderBy(performanceCase => performanceCase.ReportedDate),
                "name" or "employee" => cases.OrderBy(performanceCase => performanceCase.Employee!.FirstName).ThenBy(performanceCase => performanceCase.Employee!.LastName),
                "status" => cases.OrderBy(performanceCase => performanceCase.Status).ThenByDescending(performanceCase => performanceCase.ReportedDate),
                "severity" => cases.OrderByDescending(performanceCase => performanceCase.Severity).ThenByDescending(performanceCase => performanceCase.ReportedDate),
                _ => cases.OrderByDescending(performanceCase => performanceCase.ReportedDate).ThenByDescending(performanceCase => performanceCase.Id)
            };

            var page = await orderedCases
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(performanceCase => new
                {
                    performanceCase.Id,
                    performanceCase.CaseNumber,
                    performanceCase.IssueType,
                    performanceCase.Severity,
                    performanceCase.Status,
                    performanceCase.ReportedDate,
                    EmployeeName = performanceCase.Employee!.FirstName + " " + performanceCase.Employee!.LastName,
                    Department = performanceCase.Employee!.Department
                })
                .ToListAsync();

            var pageIds = page.Select(item => item.Id).ToList();
            var meetingStats = await _dbContext.PerformanceMeetings.AsNoTracking()
                .Where(meeting => meeting.IsActive && pageIds.Contains(meeting.PerformanceCaseId))
                .GroupBy(meeting => meeting.PerformanceCaseId)
                .Select(group => new { Id = group.Key, Value = group.Count() }).ToListAsync();
            var warningStats = await _dbContext.PerformanceWarnings.AsNoTracking()
                .Where(warning => warning.IsActive && pageIds.Contains(warning.PerformanceCaseId))
                .GroupBy(warning => warning.PerformanceCaseId)
                .Select(group => new { Id = group.Key, Value = group.Count() }).ToListAsync();
            var planStats = await _dbContext.ImprovementPlans.AsNoTracking()
                .Where(plan => plan.IsActive && pageIds.Contains(plan.PerformanceCaseId))
                .GroupBy(plan => plan.PerformanceCaseId)
                .Select(group => new { Id = group.Key, Value = group.Count() }).ToListAsync();

            var meetingLookup = meetingStats.ToDictionary(item => item.Id, item => item.Value);
            var warningLookup = warningStats.ToDictionary(item => item.Id, item => item.Value);
            var planLookup = planStats.ToDictionary(item => item.Id, item => item.Value);

            model.Rows = page.Select(item => Row(
            [
                item.CaseNumber,
                item.EmployeeName,
                item.Department,
                item.IssueType.GetDisplayName(),
                item.Severity.GetDisplayName(),
                item.Status.GetDisplayName(),
                item.ReportedDate.ToString("dd MMM yyyy"),
                (meetingLookup.TryGetValue(item.Id, out var meetingsCount) ? meetingsCount : 0).ToString("N0"),
                (warningLookup.TryGetValue(item.Id, out var warningsCount) ? warningsCount : 0).ToString("N0"),
                (planLookup.TryGetValue(item.Id, out var plansCount) ? plansCount : 0).ToString("N0")
            ], item.Status.GetDisplayName(), item.Status.GetBadgeClass(), Url.Action("Details", "Performance", new { id = item.Id }))).ToList();

            var topIssue = byIssue.OrderByDescending(item => item.Value).FirstOrDefault();
            var topSeverity = bySeverity.OrderByDescending(item => item.Value).FirstOrDefault();

            model.Insights =
            [
                Insight("Most common issue", topIssue?.Status.GetDisplayName() ?? "No data",
                    topIssue == null ? "No cases in scope" : $"{topIssue.Value:N0} cases", "bi-tags"),
                Insight("Highest severity", topSeverity?.Status.GetDisplayName() ?? "No data",
                    topSeverity == null ? "No cases in scope" : $"{topSeverity.Value:N0} cases", "bi-exclamation-diamond", "warning"),
                Insight("Plan follow-up", $"{overduePlans:N0} overdue",
                    overduePlans > 0 ? "Improvement plans past their end date" : "No overdue improvement plans",
                    "bi-alarm", overduePlans > 0 ? "risk" : "positive"),
                Insight("Meeting follow-ups", $"{followUps:N0} scheduled",
                    followUps > 0 ? "Meetings with a follow-up date recorded" : "No meeting follow-ups recorded",
                    "bi-calendar-check")
            ];

            return model;
        }

        // ==================================================================
        // Termination report
        // ==================================================================

        [HttpGet]
        public async Task<IActionResult> Termination([FromQuery] HRReportFilterViewModel filter)
            => View(await BuildTerminationAsync(filter));

        private async Task<HRReportPageViewModel> BuildTerminationAsync(HRReportFilterViewModel filter, bool export = false)
        {
            var model = BasePage("termination", nameof(Termination), filter, export);
            model.Title = "Termination Report";
            model.Description = "Offboarding requests, approver decisions and clearance progress.";
            model.Icon = "bi-box-arrow-right";
            model.StatusLabel = "Request Status";
            model.CategoryLabel = "Termination Type";
            model.StatusOptions = Enum.GetValues<TerminationStatus>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();
            model.CategoryOptions = Enum.GetValues<TerminationType>()
                .Select(value => new SelectListItem { Value = value.ToString(), Text = value.GetDisplayName() }).ToList();

            var requests = _dbContext.TerminationRequests.AsNoTracking()
                .Where(request => request.IsActive
                    && request.RequestDate >= model.RangeStart
                    && request.RequestDate <= model.RangeEnd);

            if (!string.IsNullOrWhiteSpace(model.Filter.Department))
            {
                var department = model.Filter.Department;
                requests = requests.Where(request => request.Employee!.Department == department);
            }

            if (Enum.TryParse<TerminationStatus>(model.Filter.Status, true, out var requestStatus))
            {
                requests = requests.Where(request => request.Status == requestStatus);
            }

            if (Enum.TryParse<TerminationType>(model.Filter.Category, true, out var terminationType))
            {
                requests = requests.Where(request => request.TerminationType == terminationType);
            }

            if (!string.IsNullOrWhiteSpace(model.Filter.Search))
            {
                var term = model.Filter.Search;
                requests = requests.Where(request =>
                    request.ReferenceNumber.Contains(term)
                    || request.Reason.Contains(term)
                    || request.Employee!.FirstName.Contains(term)
                    || request.Employee!.LastName.Contains(term));
            }

            var requestIds = requests.Select(request => request.Id);
            var totalRequests = await requests.CountAsync();
            var pendingApproval = await requests.CountAsync(request => request.Status == TerminationStatus.PendingApproval);
            var approved = await requests.CountAsync(request => request.Status == TerminationStatus.Approved);
            var inProgress = await requests.CountAsync(request => request.Status == TerminationStatus.InProgress);
            var completed = await requests.CountAsync(request => request.Status == TerminationStatus.Completed);
            var rejected = await requests.CountAsync(request => request.Status == TerminationStatus.Rejected);
            var pendingClearance = await requests.CountAsync(request =>
                (request.Status == TerminationStatus.Approved || request.Status == TerminationStatus.InProgress)
                && (!request.ClearanceItems.Any(item => item.IsActive)
                    || request.ClearanceItems.Any(item => item.IsActive && item.IsMandatory && item.Status != ClearanceStatus.Cleared)));

            var clearanceItems = _dbContext.ClearanceItems.AsNoTracking()
                .Where(item => item.IsActive && requestIds.Contains(item.TerminationRequestId));
            var totalItems = await clearanceItems.CountAsync();
            var clearedItems = await clearanceItems.CountAsync(item => item.Status == ClearanceStatus.Cleared);

            model.Kpis =
            [
                Kpi("Total Requests", totalRequests.ToString("N0"), "Requests in scope", "bi-collection"),
                Kpi("Pending Approval", pendingApproval.ToString("N0"), "Awaiting a decision", "bi-hourglass-split", pendingApproval > 0 ? "warning" : string.Empty),
                Kpi("Approved", approved.ToString("N0"), "Approved, not started", "bi-check2-square"),
                Kpi("In Progress", inProgress.ToString("N0"), "Clearance underway", "bi-arrow-repeat"),
                Kpi("Completed", completed.ToString("N0"), "Fully offboarded", "bi-flag", "positive"),
                Kpi("Rejected", rejected.ToString("N0"), "Declined requests", "bi-x-circle", rejected > 0 ? "warning" : string.Empty),
                Kpi("Pending Clearance", pendingClearance.ToString("N0"), "Blocked on clearance", "bi-clipboard-check", pendingClearance > 0 ? "risk" : string.Empty),
                Kpi("Clearance Completion", Percent(clearedItems, totalItems), $"{clearedItems:N0} of {totalItems:N0} items", "bi-clipboard2-check", "positive")
            ];

            var byStatus = await requests.GroupBy(request => request.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var byType = await requests.GroupBy(request => request.TerminationType)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();
            var itemsByStatus = await clearanceItems.GroupBy(item => item.Status)
                .Select(group => new { Status = group.Key, Value = group.Count() }).ToListAsync();

            var completedDates = (await requests
                .Where(request => request.Status == TerminationStatus.Completed && request.CompletedAt != null)
                .Select(request => request.CompletedAt)
                .ToListAsync())
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();
            var completedByMonth = completedDates
                .GroupBy(date => new DateOnly(date.Year, date.Month, 1))
                .OrderBy(group => group.Key)
                .Select(group => new { Label = group.Key.ToString("MMM yyyy"), Value = group.Count() })
                .ToList();

            model.Charts =
            [
                Chart("hrTermStatus", "Requests by Status", "Offboarding lifecycle", "doughnut",
                    byStatus.Select(item => item.Status.GetDisplayName()), byStatus.Select(item => (decimal)item.Value)),
                Chart("hrTermType", "Requests by Type", "Reason distribution", "bar",
                    byType.Select(item => item.Status.GetDisplayName()), byType.Select(item => (decimal)item.Value)),
                Chart("hrTermClearance", "Clearance Items by Status", "Checklist progress", "doughnut",
                    itemsByStatus.Select(item => item.Status.GetDisplayName()), itemsByStatus.Select(item => (decimal)item.Value)),
                Chart("hrTermCompleted", "Completed by Month", "Offboarding completed over the selected range", "line",
                    completedByMonth.Select(item => item.Label), completedByMonth.Select(item => (decimal)item.Value))
            ];

            model.TotalRecords = totalRequests;
            CountPages(model);
            model.Columns = ["Reference", "Employee", "Department", "Type", "Requested", "Last Working Day", "Status", "Clearance", "Letter"];

            var orderedRequests = model.Filter.SortBy switch
            {
                "oldest" => requests.OrderBy(request => request.RequestDate),
                "name" or "employee" => requests.OrderBy(request => request.Employee!.FirstName).ThenBy(request => request.Employee!.LastName),
                "status" => requests.OrderBy(request => request.Status).ThenByDescending(request => request.RequestDate),
                _ => requests.OrderByDescending(request => request.RequestDate).ThenByDescending(request => request.Id)
            };

            var page = await orderedRequests
                .Skip((model.CurrentPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(request => new
                {
                    request.Id,
                    request.ReferenceNumber,
                    request.TerminationType,
                    request.RequestDate,
                    request.ProposedLastWorkingDay,
                    request.Status,
                    EmployeeName = request.Employee!.FirstName + " " + request.Employee!.LastName,
                    Department = request.Employee!.Department
                })
                .ToListAsync();

            var pageIds = page.Select(item => item.Id).ToList();
            var clearanceStats = await _dbContext.ClearanceItems.AsNoTracking()
                .Where(item => item.IsActive && pageIds.Contains(item.TerminationRequestId))
                .GroupBy(item => item.TerminationRequestId)
                .Select(group => new
                {
                    Id = group.Key,
                    Total = group.Count(),
                    Cleared = group.Count(item => item.Status == ClearanceStatus.Cleared)
                })
                .ToListAsync();
            var letterStats = await _dbContext.TerminationDocuments.AsNoTracking()
                .Where(document => document.IsLetter && pageIds.Contains(document.TerminationRequestId))
                .GroupBy(document => document.TerminationRequestId)
                .Select(group => new { Id = group.Key, Value = group.Count() }).ToListAsync();

            var clearanceLookup = clearanceStats.ToDictionary(item => item.Id);
            var letterLookup = letterStats.ToDictionary(item => item.Id, item => item.Value);

            model.Rows = page.Select(item =>
            {
                var stats = clearanceLookup.TryGetValue(item.Id, out var found) ? found : null;
                var total = stats?.Total ?? 0;
                var cleared = stats?.Cleared ?? 0;
                return Row(
                [
                    item.ReferenceNumber,
                    item.EmployeeName,
                    item.Department,
                    item.TerminationType.GetDisplayName(),
                    item.RequestDate.ToString("dd MMM yyyy"),
                    item.ProposedLastWorkingDay.ToString("dd MMM yyyy"),
                    item.Status.GetDisplayName(),
                    total == 0 ? "Not started" : $"{cleared}/{total} cleared",
                    letterLookup.ContainsKey(item.Id) ? "Available" : "Missing"
                ], item.Status.GetDisplayName(), item.Status.GetBadgeClass(), Url.Action("Details", "Termination", new { id = item.Id }));
            }).ToList();

            var topType = byType.OrderByDescending(item => item.Value).FirstOrDefault();

            model.Insights =
            [
                Insight("Most common reason", topType?.Status.GetDisplayName() ?? "No data",
                    topType == null ? "No requests in scope" : $"{topType.Value:N0} requests", "bi-box-arrow-right"),
                Insight("Clearance progress", Percent(clearedItems, totalItems),
                    $"{clearedItems:N0} of {totalItems:N0} clearance items cleared", "bi-clipboard2-check", "positive"),
                Insight("Attention needed", $"{pendingClearance:N0} blocked",
                    pendingClearance > 0 ? "Approved or in-progress requests with uncleared items" : "No requests blocked on clearance",
                    "bi-exclamation-triangle", pendingClearance > 0 ? "risk" : "positive")
            ];

            return model;
        }

        // ==================================================================
        // CSV export
        // ==================================================================

        [HttpGet]
        public async Task<IActionResult> Export(string report, [FromQuery] HRReportFilterViewModel filter)
        {
            filter.Page = 1;
            filter.PageSize = 50000;

            var model = (report ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "recruitment" => await BuildRecruitmentAsync(filter, true),
                "employees" => await BuildEmployeesAsync(filter, true),
                "performance" => await BuildPerformanceAsync(filter, true),
                "termination" => await BuildTerminationAsync(filter, true),
                _ => null
            };

            if (model == null)
            {
                return RedirectToAction("NotFound", "Error");
            }

            var generatedBy = User.FindFirstValue(ClaimTypes.Name) ?? "GameHub User";
            return Csv(model, generatedBy);
        }

        private static FileContentResult Csv(HRReportPageViewModel model, string generatedBy)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CsvLine(["GameHub HR Reports", model.Title]));
            builder.AppendLine(CsvLine(["Generated", model.RefreshedAt.ToString("dd MMM yyyy HH:mm"), "Generated By", generatedBy]));
            builder.AppendLine(CsvLine(["Filters", model.FilterSummary]));
            builder.AppendLine();
            builder.AppendLine(CsvLine(model.Columns));
            foreach (var row in model.Rows)
            {
                builder.AppendLine(CsvLine(row.Cells));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return new FileContentResult(bytes, "text/csv")
            {
                FileDownloadName = $"GameHub-HR-{model.ReportKey}-{DateTime.UtcNow:yyyyMMdd}.csv"
            };
        }

        private static string CsvLine(IEnumerable<string> values)
            => string.Join(",", values.Select(value => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\""));

        // ==================================================================
        // Filtering / paging plumbing
        // ==================================================================

        private HRReportPageViewModel BasePage(string reportKey, string actionName, HRReportFilterViewModel filter, bool export)
        {
            filter.Preset = string.IsNullOrWhiteSpace(filter.Preset) ? "all" : filter.Preset.Trim().ToLowerInvariant();
            filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
            filter.Department = string.IsNullOrWhiteSpace(filter.Department) ? null : filter.Department.Trim();
            filter.Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
            filter.Category = string.IsNullOrWhiteSpace(filter.Category) ? null : filter.Category.Trim();
            filter.SortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "newest" : filter.SortBy.Trim().ToLowerInvariant();

            if (export)
            {
                filter.Page = 1;
            }
            else
            {
                filter.PageSize = PageSizes.Contains(filter.PageSize) ? filter.PageSize : 10;
                filter.Page = Math.Max(1, filter.Page);
            }

            var (start, end) = ResolveRange(filter);
            if (!string.Equals(filter.Preset, "all", StringComparison.OrdinalIgnoreCase))
            {
                filter.StartDate = start;
                filter.EndDate = end;
            }
            else
            {
                filter.StartDate = null;
                filter.EndDate = null;
            }

            return new HRReportPageViewModel
            {
                ReportKey = reportKey,
                ActionName = actionName,
                Filter = filter,
                RangeStart = start,
                RangeEnd = end,
                RefreshedAt = DateTime.UtcNow,
                Departments = HRLists.Departments,
                CurrentPage = filter.Page,
                PageSize = filter.PageSize,
                FilterSummary = FilterSummary(filter, start, end),
                HasFilters = HasActiveFilters(filter)
            };
        }

        private static DateTime PakistanNow()
        {
            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time"));
            }
            catch
            {
                return DateTime.UtcNow.AddHours(5);
            }
        }

        private static (DateOnly Start, DateOnly End) ResolveRange(HRReportFilterViewModel filter)
        {
            var today = DateOnly.FromDateTime(PakistanNow());
            var start = new DateOnly(1900, 1, 1);
            var end = today;

            switch (filter.Preset)
            {
                case "today":
                    start = end = today;
                    break;
                case "this-week":
                    start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                    end = today;
                    break;
                case "this-month":
                    start = new DateOnly(today.Year, today.Month, 1);
                    end = today;
                    break;
                case "last-month":
                    var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
                    end = firstOfThisMonth.AddDays(-1);
                    start = new DateOnly(end.Year, end.Month, 1);
                    break;
                case "this-quarter":
                    var firstMonthOfQuarter = ((today.Month - 1) / 3) * 3 + 1;
                    start = new DateOnly(today.Year, firstMonthOfQuarter, 1);
                    end = today;
                    break;
                case "this-year":
                    start = new DateOnly(today.Year, 1, 1);
                    end = today;
                    break;
                case "last-year":
                    start = new DateOnly(today.Year - 1, 1, 1);
                    end = new DateOnly(today.Year - 1, 12, 31);
                    break;
                case "custom":
                    start = filter.StartDate ?? start;
                    end = filter.EndDate ?? end;
                    break;
            }

            if (end < start)
            {
                (start, end) = (end, start);
            }

            return (start, end);
        }

        private static void CountPages(HRReportPageViewModel model)
        {
            model.TotalPages = model.TotalRecords == 0
                ? 1
                : (int)Math.Ceiling(model.TotalRecords / (double)model.PageSize);
            model.CurrentPage = Math.Clamp(model.CurrentPage, 1, Math.Max(1, model.TotalPages));
        }

        private static bool HasActiveFilters(HRReportFilterViewModel filter)
            => !string.Equals(filter.Preset, "all", StringComparison.OrdinalIgnoreCase)
               || !string.IsNullOrWhiteSpace(filter.Department)
               || !string.IsNullOrWhiteSpace(filter.Status)
               || !string.IsNullOrWhiteSpace(filter.Category)
               || !string.IsNullOrWhiteSpace(filter.Search);

        private static string FilterSummary(HRReportFilterViewModel filter, DateOnly start, DateOnly end)
        {
            var parts = new List<string>();
            if (string.Equals(filter.Preset, "all", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("All time");
            }
            else if (start == end)
            {
                parts.Add(start.ToString("dd MMM yyyy"));
            }
            else
            {
                parts.Add($"{start:dd MMM yyyy} – {end:dd MMM yyyy}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Department)) parts.Add(filter.Department!);
            if (!string.IsNullOrWhiteSpace(filter.Status)) parts.Add(filter.Status!);
            if (!string.IsNullOrWhiteSpace(filter.Category)) parts.Add(filter.Category!);
            if (!string.IsNullOrWhiteSpace(filter.Search)) parts.Add($"“{filter.Search}”");

            return string.Join(" · ", parts);
        }

        private static string Tenure(DateOnly joiningDate)
        {
            var today = DateOnly.FromDateTime(PakistanNow());
            if (joiningDate > today)
            {
                return "—";
            }

            var months = ((today.Year - joiningDate.Year) * 12) + today.Month - joiningDate.Month;
            if (today.Day < joiningDate.Day)
            {
                months--;
            }

            if (months < 0) months = 0;
            var years = months / 12;
            var remainingMonths = months % 12;

            if (years == 0) return $"{months} mo";
            return remainingMonths == 0 ? $"{years} yr" : $"{years} yr {remainingMonths} mo";
        }

        // ==================================================================
        // Small presentation helpers
        // ==================================================================

        private static ReportKpiViewModel Kpi(string label, string value, string helper, string icon, string tone = "")
            => new() { Label = label, Value = value, Helper = helper, Icon = icon, Tone = tone };

        private static ReportInsightViewModel Insight(string label, string value, string detail, string icon, string tone = "")
            => new() { Label = label, Value = value, Detail = detail, Icon = icon, Tone = tone };

        private static ReportChartViewModel Chart(string id, string title, string subtitle, string type, IEnumerable<string> labels, IEnumerable<decimal> values)
            => new()
            {
                Id = id,
                Title = title,
                Subtitle = subtitle,
                Type = type,
                Labels = labels.ToList(),
                Values = values.ToList(),
                Colors = ChartColors
            };

        private static HRReportTableRowViewModel Row(IReadOnlyList<string> cells, string? status = null, string? tone = null, string? url = null)
            => new() { Cells = cells, Status = status, Tone = tone, DetailUrl = url };

        private static ReportModuleLinkViewModel Module(string title, string description, string action, string icon)
            => new() { Title = title, Description = description, Action = action, Icon = icon, Available = true };

        private static string Percent(int value, int total) => total == 0 ? "0.0%" : $"{value * 100m / total:N1}%";
    }
}
