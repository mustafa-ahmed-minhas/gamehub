using System.Globalization;
using System.Security.Claims;
using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [ModuleAuthorize(RolePermissions.Modules.Hr)]
    [ValidateActiveUser]
    public class HRController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public HRController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Shared dashboard palette (mirrors the HR Reports chart palette so the
        // whole HR module reads as one visual family).
        private static readonly string[] ChartColors =
        {
            "#d6a652", "#171614", "#4f8b6d", "#c77b4f", "#7b8494",
            "#ead09a", "#8e6640", "#9d4f4f", "#6b7f8c", "#a9b389"
        };

        // Screen 1 — HR Dashboard. Every figure, percentage and comparison is
        // aggregated from live Employee, Job Posting, Job Application, Interview
        // and Job Offer records; there is no placeholder data.
        public async Task<IActionResult> Index()
        {
            var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? "User";
            var hour = DateTime.Now.Hour;
            var greeting = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening";

            return View(await BuildDashboardAsync(greeting, firstName));
        }

        private async Task<HRDashboardViewModel> BuildDashboardAsync(string greeting, string firstName)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // ---- Live source rows -------------------------------------------
            var employees = await _dbContext.Employees.AsNoTracking()
                .Where(employee => employee.IsActive)
                .Select(employee => new
                {
                    employee.Department,
                    employee.EmploymentType,
                    employee.EmploymentStatus,
                    employee.JoiningDate
                })
                .ToListAsync();

            var applications = await _dbContext.JobApplications.AsNoTracking()
                .Where(application => application.IsActive)
                .Select(application => new
                {
                    application.Status,
                    application.ApplicationDate
                })
                .ToListAsync();

            var interviews = await _dbContext.Interviews.AsNoTracking()
                .Where(interview => interview.IsActive)
                .Select(interview => new
                {
                    interview.Status,
                    interview.InterviewDate
                })
                .ToListAsync();

            var postings = await _dbContext.JobPostings.AsNoTracking()
                .Where(posting => posting.IsActive)
                .Select(posting => new
                {
                    posting.Department,
                    posting.Status,
                    posting.ApplicationDeadline,
                    posting.CreatedAt,
                    posting.Vacancies
                })
                .ToListAsync();

            var offerStatuses = await _dbContext.JobOffers.AsNoTracking()
                .Where(offer => offer.IsActive)
                .Select(offer => offer.Status)
                .ToListAsync();

            // ---- Workforce metrics ------------------------------------------
            var totalEmployees = employees.Count;
            var activeEmployees = employees.Count(employee => employee.EmploymentStatus == EmploymentStatus.Active);
            var onLeaveEmployees = employees.Count(employee => employee.EmploymentStatus == EmploymentStatus.OnLeave);
            var inactiveEmployees = employees.Count(employee => employee.EmploymentStatus == EmploymentStatus.Inactive);
            var activeShare = PercentValue(activeEmployees, totalEmployees);
            var averageTenureMonths = activeEmployees == 0
                ? 0
                : employees.Where(employee => employee.EmploymentStatus == EmploymentStatus.Active)
                    .Average(employee => MonthsBetween(employee.JoiningDate, today));

            // Recruitment metrics
            var openPostings = postings.Count(posting => posting.Status == JobPostingStatus.Open
                && posting.ApplicationDeadline >= today);
            var draftPostings = postings.Count(posting => posting.Status == JobPostingStatus.Draft);
            var closingSoon = postings.Count(posting => posting.Status == JobPostingStatus.Open
                && posting.ApplicationDeadline >= today
                && posting.ApplicationDeadline <= today.AddDays(7));
            var closingFortnight = postings.Count(posting => posting.Status == JobPostingStatus.Open
                && posting.ApplicationDeadline >= today
                && posting.ApplicationDeadline <= today.AddDays(14));

            var submittedCount = applications.Count(application => application.Status == JobApplicationStatus.Submitted);
            var screeningCount = applications.Count(application => application.Status == JobApplicationStatus.Screening);
            var awaitingReview = submittedCount + screeningCount;
            var totalApplications = applications.Count;

            var shortlistedWithoutInterview = await _dbContext.JobApplications.AsNoTracking()
                .CountAsync(application => application.IsActive
                    && application.Status == JobApplicationStatus.Shortlisted
                    && !application.Interviews.Any(interview => interview.IsActive));

            var upcomingInterviewDates = interviews
                .Where(interview => interview.Status == InterviewStatus.Scheduled
                    && interview.InterviewDate >= today)
                .Select(interview => interview.InterviewDate)
                .OrderBy(date => date)
                .ToList();
            var upcomingInterviews = upcomingInterviewDates.Count;
            var interviewsThisWeek = upcomingInterviewDates.Count(date => date <= today.AddDays(7));
            var overdueInterviews = interviews.Count(interview => interview.Status == InterviewStatus.Scheduled
                && interview.InterviewDate < today);
            var nextInterview = upcomingInterviews > 0 ? upcomingInterviewDates[0] : (DateOnly?)null;

            var pendingHrActions = awaitingReview + shortlistedWithoutInterview + closingSoon + overdueInterviews;

            // ---- Trend comparisons (real period deltas) ---------------------
            var joinersLast90 = employees.Count(employee => employee.JoiningDate <= today
                && employee.JoiningDate > today.AddDays(-90));
            var joinersPrior90 = employees.Count(employee => employee.JoiningDate <= today.AddDays(-90)
                && employee.JoiningDate > today.AddDays(-180));
            var joinerDelta = joinersLast90 - joinersPrior90;

            var appsLast30 = applications.Count(application => application.ApplicationDate <= today
                && application.ApplicationDate > today.AddDays(-30));
            var appsPrior30 = applications.Count(application => application.ApplicationDate <= today.AddDays(-30)
                && application.ApplicationDate > today.AddDays(-60));
            var appsDelta = appsLast30 - appsPrior30;

            var postingsLast30 = postings.Count(posting => posting.CreatedAt <= DateTime.UtcNow
                && posting.CreatedAt > DateTime.UtcNow.AddDays(-30));
            var postingsPrior30 = postings.Count(posting => posting.CreatedAt <= DateTime.UtcNow.AddDays(-30)
                && posting.CreatedAt > DateTime.UtcNow.AddDays(-60));
            var postingsDelta = postingsLast30 - postingsPrior30;

            var offersIssued = offerStatuses.Count(status => status != JobOfferStatus.Draft);
            var offersAccepted = offerStatuses.Count(status =>
                status == JobOfferStatus.Accepted || status == JobOfferStatus.Hired);
            var offerAcceptanceRate = PercentValue(offersAccepted, offersIssued);

            // ---- Charts -----------------------------------------------------
            var departments = employees
                .GroupBy(employee => employee.Department)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .ToList();

            var types = employees
                .GroupBy(employee => employee.EmploymentType)
                .OrderByDescending(group => group.Count())
                .ToList();

            var openRolesByDepartment = postings
                .Where(posting => posting.Status == JobPostingStatus.Open
                    && posting.ApplicationDeadline >= today)
                .GroupBy(posting => posting.Department)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => new { Department = group.Key, Vacancies = group.Sum(posting => posting.Vacancies) })
                .OrderByDescending(group => group.Vacancies)
                .ThenBy(group => group.Department)
                .ToList();

            var months = RecentMonths(6, today);
            var applicationsByMonth = months.Select(month => (decimal)applications.Count(application =>
                application.ApplicationDate.Year == month.Year
                && application.ApplicationDate.Month == month.Month)).ToList();
            var interviewsByMonth = months.Select(month => (decimal)interviews.Count(interview =>
                interview.InterviewDate.Year == month.Year
                && interview.InterviewDate.Month == month.Month)).ToList();
            var joinersByMonth = months.Select(month => (decimal)employees.Count(employee =>
                employee.JoiningDate.Year == month.Year
                && employee.JoiningDate.Month == month.Month)).ToList();

            var charts = new List<HRDashboardChartViewModel>
            {
                new()
                {
                    Id = "hrDashDepartments",
                    Title = "Department Distribution",
                    Subtitle = "Employee headcount by department",
                    Type = "doughnut",
                    Labels = departments.Select(group => group.Key).ToList(),
                    Series = new[] { Series("Employees", departments.Select(group => (decimal)group.Count()).ToList()) },
                    Colors = AssignColors(departments.Count),
                    CenterValue = totalEmployees.ToString(CultureInfo.InvariantCulture),
                    CenterLabel = "Employees",
                    ShowLegend = true
                },
                new()
                {
                    Id = "hrDashStatus",
                    Title = "Employment Status",
                    Subtitle = "Active, on leave and inactive records",
                    Type = "doughnut",
                    Labels = new[] { "Active", "On Leave", "Inactive" },
                    Series = new[] { Series("Employees", new List<decimal> { activeEmployees, onLeaveEmployees, inactiveEmployees }) },
                    Colors = new[] { "#4f8b6d", "#d6a652", "#9d4f4f" },
                    CenterValue = totalEmployees.ToString(CultureInfo.InvariantCulture),
                    CenterLabel = "Total",
                    ShowLegend = true
                },
                new()
                {
                    Id = "hrDashTypes",
                    Title = "Employment Mix",
                    Subtitle = "Workforce by engagement type",
                    Type = "doughnut",
                    Labels = types.Select(group => group.Key.GetDisplayName()).ToList(),
                    Series = new[] { Series("Employees", types.Select(group => (decimal)group.Count()).ToList()) },
                    Colors = AssignColors(types.Count),
                    CenterValue = $"{activeShare:0.#}%",
                    CenterLabel = "Active",
                    ShowLegend = true
                },
                new()
                {
                    Id = "hrDashHiringTrend",
                    Title = "Recruitment Momentum",
                    Subtitle = "Applications received vs interviews held over the last 6 months",
                    Type = "bar",
                    Labels = months.Select(month => month.ToString("MMM", CultureInfo.InvariantCulture)).ToList(),
                    Series = new[]
                    {
                        new HRDashboardChartSeries { Label = "Applications", Type = "bar", Values = applicationsByMonth, Color = "#d6a652" },
                        new HRDashboardChartSeries { Label = "Interviews", Type = "line", Values = interviewsByMonth, Color = "#171614", Fill = false }
                    },
                    ShowLegend = true
                },
                new()
                {
                    Id = "hrDashJoiners",
                    Title = "New Joiners",
                    Subtitle = "Employees who joined over the last 6 months",
                    Type = "line",
                    Labels = months.Select(month => month.ToString("MMM", CultureInfo.InvariantCulture)).ToList(),
                    Series = new[]
                    {
                        new HRDashboardChartSeries { Label = "New joiners", Type = "line", Values = joinersByMonth, Color = "#4f8b6d", Fill = true }
                    },
                    ShowLegend = false
                },
                new()
                {
                    Id = "hrDashOpenings",
                    Title = "Open Roles by Department",
                    Subtitle = "Vacancies across currently open postings",
                    Type = "bar",
                    Horizontal = true,
                    Labels = openRolesByDepartment.Select(item => item.Department).ToList(),
                    Series = new[]
                    {
                        new HRDashboardChartSeries
                        {
                            Label = "Vacancies",
                            Type = "bar",
                            Values = openRolesByDepartment.Select(item => (decimal)item.Vacancies).ToList(),
                            Color = "#4f8b6d"
                        }
                    },
                    ShowLegend = false
                }
            };

            // ---- Recruitment pipeline ---------------------------------------
            var pipelineStages = new[]
            {
                (JobApplicationStatus.Submitted, "#7b8494"),
                (JobApplicationStatus.Screening, "#c77b4f"),
                (JobApplicationStatus.Shortlisted, "#a9822f"),
                (JobApplicationStatus.Interview, "#d6a652"),
                (JobApplicationStatus.Offer, "#4f8b6d"),
                (JobApplicationStatus.Hired, "#2f6b4f"),
                (JobApplicationStatus.Rejected, "#9d4f4f")
            };
            var pipeline = pipelineStages
                .Select(stage => new HRPipelineStageViewModel
                {
                    Label = stage.Item1.GetDisplayName(),
                    Tone = stage.Item1.GetBadgeClass(),
                    Count = applications.Count(application => application.Status == stage.Item1),
                    Percent = PercentValue(applications.Count(application => application.Status == stage.Item1), totalApplications),
                    Color = stage.Item2
                })
                .ToList();

            // ---- Insights (all derived from the same live rows) -------------
            var insights = new List<ReportInsightViewModel>();
            if (departments.Count > 0)
            {
                var largest = departments[0];
                insights.Add(Insight(
                    "Largest Department", largest.Key,
                    $"{largest.Count()} employees · {PercentValue(largest.Count(), totalEmployees):0.#}% of workforce",
                    "bi-diagram-3", "gold"));
            }
            insights.Add(Insight(
                "Offer Acceptance", $"{offerAcceptanceRate:0.#}%",
                offersIssued == 0
                    ? "No offers issued yet"
                    : $"{offersAccepted} of {offersIssued} offers accepted or hired",
                "bi-patch-check", "positive"));
            insights.Add(Insight(
                "Average Tenure", $"{averageTenureMonths / 12:0.#} yrs",
                $"Across {activeEmployees} active employees",
                "bi-hourglass-split", "neutral"));
            insights.Add(Insight(
                "On Leave Today", onLeaveEmployees.ToString(CultureInfo.InvariantCulture),
                $"{PercentValue(onLeaveEmployees, totalEmployees):0.#}% of the workforce",
                "bi-calendar2-week", onLeaveEmployees > 0 ? "warning" : "positive"));

            var model = new HRDashboardViewModel
            {
                Greeting = greeting,
                FirstName = firstName,
                RefreshedAt = DateTime.Now,
                Kpis = BuildKpis(
                    totalEmployees, activeEmployees, inactiveEmployees, activeShare,
                    onLeaveEmployees, joinerDelta,
                    openPostings, closingFortnight, draftPostings, postingsDelta,
                    awaitingReview, submittedCount, screeningCount, appsDelta,
                    upcomingInterviews, nextInterview, interviewsThisWeek,
                    pendingHrActions, closingSoon, overdueInterviews),
                Charts = charts,
                Pipeline = pipeline,
                PipelineTotal = totalApplications,
                Insights = insights,
                TotalApplications = totalApplications
            };

            model.RecentRecruitment = await BuildRecentRecruitmentAsync();
            model.PendingTasks = BuildPendingTasks(awaitingReview, shortlistedWithoutInterview, closingSoon, overdueInterviews);

            return model;
        }

        private static IReadOnlyList<HRDashboardKpiViewModel> BuildKpis(
            int totalEmployees, int activeEmployees, int inactiveEmployees, double activeShare,
            int onLeaveEmployees, int joinerDelta,
            int openPostings, int closingFortnight, int draftPostings, int postingsDelta,
            int awaitingReview, int submittedCount, int screeningCount, int appsDelta,
            int upcomingInterviews, DateOnly? nextInterview, int interviewsThisWeek,
            int pendingHrActions, int closingSoon, int overdueInterviews)
        {
            return new List<HRDashboardKpiViewModel>
            {
                Kpi(
                    "Total Employees", totalEmployees.ToString(CultureInfo.InvariantCulture),
                    $"{activeEmployees} active · {inactiveEmployees} inactive",
                    "bi-people", "gold",
                    SignedDelta(joinerDelta, "vs prior 90d"),
                    joinerDelta > 0 ? "positive" : joinerDelta < 0 ? "negative" : "neutral",
                    joinerDelta > 0 ? "bi-arrow-up-right" : joinerDelta < 0 ? "bi-arrow-down-right" : "bi-dash"),
                Kpi(
                    "Active Employees", activeEmployees.ToString(CultureInfo.InvariantCulture),
                    $"{activeShare:0.#}% of all records · {onLeaveEmployees} on leave",
                    "bi-person-check", "positive",
                    $"{activeShare:0.#}%",
                    "neutral", "bi-pie-chart"),
                Kpi(
                    "Open Positions", openPostings.ToString(CultureInfo.InvariantCulture),
                    $"{closingFortnight} closing in 14 days · {draftPostings} draft",
                    "bi-briefcase", "gold",
                    SignedDelta(postingsDelta, "vs prior 30d"),
                    postingsDelta > 0 ? "positive" : postingsDelta < 0 ? "negative" : "neutral",
                    postingsDelta > 0 ? "bi-arrow-up-right" : postingsDelta < 0 ? "bi-arrow-down-right" : "bi-dash"),
                Kpi(
                    "Pending Applications", awaitingReview.ToString(CultureInfo.InvariantCulture),
                    $"{submittedCount} submitted · {screeningCount} screening",
                    "bi-inbox", "warning",
                    SignedDelta(appsDelta, "vs prior 30d"),
                    "neutral", appsDelta >= 0 ? "bi-arrow-up-right" : "bi-arrow-down-right"),
                Kpi(
                    "Upcoming Interviews", upcomingInterviews.ToString(CultureInfo.InvariantCulture),
                    nextInterview.HasValue
                        ? $"Next on {nextInterview.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} · {interviewsThisWeek} this week"
                        : $"Nothing scheduled · {interviewsThisWeek} this week",
                    "bi-calendar-event", "info",
                    $"{interviewsThisWeek} this week",
                    "neutral", "bi-calendar-week"),
                Kpi(
                    "Pending HR Actions", pendingHrActions.ToString(CultureInfo.InvariantCulture),
                    $"{awaitingReview} review · {closingSoon} closing · {overdueInterviews} overdue",
                    "bi-exclamation-diamond", pendingHrActions > 0 ? "risk" : "positive",
                    pendingHrActions > 0 ? "Needs review" : "All clear",
                    pendingHrActions > 0 ? "negative" : "positive",
                    pendingHrActions > 0 ? "bi-exclamation-circle" : "bi-check2-circle")
            };
        }

        private async Task<List<HRRecruitmentActivityItem>> BuildRecentRecruitmentAsync()
        {
            var recent = await _dbContext.JobApplications.AsNoTracking()
                .Where(application => application.IsActive)
                .OrderByDescending(application => application.UpdatedAt ?? application.CreatedAt)
                .Take(7)
                .Select(application => new
                {
                    application.FirstName,
                    application.LastName,
                    application.ApplicationNumber,
                    Position = application.JobPosting != null ? application.JobPosting.JobTitle : "Position removed",
                    application.Status,
                    application.ApplicationDate,
                    UpdatedAt = application.UpdatedAt ?? application.CreatedAt
                })
                .ToListAsync();

            return recent.Select(item =>
            {
                var name = $"{item.FirstName} {item.LastName}".Trim();
                return new HRRecruitmentActivityItem
                {
                    Candidate = name,
                    Initials = Initials(name),
                    Reference = item.ApplicationNumber,
                    Position = item.Position,
                    Stage = item.Status.GetDisplayName(),
                    StageClass = item.Status.GetBadgeClass(),
                    Applied = item.ApplicationDate.ToString("dd MMM yyyy"),
                    LastUpdate = RelativeTime(item.UpdatedAt)
                };
            }).ToList();
        }

        private static List<HRTaskItem> BuildPendingTasks(
            int awaitingReview, int shortlistedWithoutInterview, int closingSoon, int overdueInterviews)
        {
            var tasks = new List<HRTaskItem>();

            if (awaitingReview > 0)
            {
                tasks.Add(TaskItem(
                    $"{awaitingReview} application{(awaitingReview == 1 ? "" : "s")} awaiting review",
                    "Recruitment", "Recruitment Team", "Today",
                    awaitingReview > 5 ? "High" : "Medium",
                    awaitingReview > 5 ? "high" : "medium",
                    "Action Needed", "action", "bi-inbox"));
            }

            if (shortlistedWithoutInterview > 0)
            {
                tasks.Add(TaskItem(
                    $"{shortlistedWithoutInterview} shortlisted candidate{(shortlistedWithoutInterview == 1 ? "" : "s")} without an interview",
                    "Recruitment", "Recruitment Team", "This week",
                    "High", "high", "Action Needed", "action", "bi-person-check"));
            }

            if (overdueInterviews > 0)
            {
                tasks.Add(TaskItem(
                    $"{overdueInterviews} past interview{(overdueInterviews == 1 ? "" : "s")} awaiting outcome",
                    "Interview", "Recruitment Team", "Today",
                    "High", "high", "Action Needed", "action", "bi-calendar-x"));
            }

            if (closingSoon > 0)
            {
                tasks.Add(TaskItem(
                    $"{closingSoon} job posting{(closingSoon == 1 ? "" : "s")} closing within 7 days",
                    "Recruitment", "Recruitment Team", "This week",
                    "Medium", "medium", "In Progress", "progress", "bi-hourglass-split"));
            }

            return tasks;
        }

        private static List<DateTime> RecentMonths(int count, DateOnly today)
        {
            var anchor = new DateTime(today.Year, today.Month, 1);
            var months = new List<DateTime>(count);
            for (var offset = count - 1; offset >= 0; offset--)
            {
                months.Add(anchor.AddMonths(-offset));
            }

            return months;
        }

        private static List<string> AssignColors(int count) =>
            Enumerable.Range(0, count).Select(index => ChartColors[index % ChartColors.Length]).ToList();

        private static HRDashboardChartSeries Series(string label, IReadOnlyList<decimal> values) =>
            new() { Label = label, Type = "doughnut", Values = values };

        private static double PercentValue(int value, int total) =>
            total <= 0 ? 0 : Math.Round(value * 100d / total, 1);

        private static int MonthsBetween(DateOnly start, DateOnly end)
        {
            if (end <= start)
            {
                return 0;
            }

            var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
            if (end.Day < start.Day)
            {
                months--;
            }

            return Math.Max(0, months);
        }

        private static string SignedDelta(int delta, string suffix) =>
            $"{(delta >= 0 ? "+" : "")}{delta} {suffix}";

        private static HRDashboardKpiViewModel Kpi(
            string label, string value, string helper, string icon, string tone,
            string trend, string trendTone, string trendIcon) => new()
        {
            Label = label,
            Value = value,
            Helper = helper,
            Icon = icon,
            Tone = tone,
            Trend = trend,
            TrendTone = trendTone,
            TrendIcon = trendIcon
        };

        private static ReportInsightViewModel Insight(
            string label, string value, string detail, string icon, string tone) => new()
        {
            Label = label,
            Value = value,
            Detail = detail,
            Icon = icon,
            Tone = tone
        };

        private static HRTaskItem TaskItem(
            string title, string category, string owner, string due,
            string priority, string priorityClass, string status, string statusClass, string icon) => new()
        {
            Title = title,
            Category = category,
            Owner = owner,
            Due = due,
            Priority = priority,
            PriorityClass = priorityClass,
            Status = status,
            StatusClass = statusClass,
            Icon = icon
        };

        private static string RelativeTime(DateTime value)
        {
            var delta = DateTime.UtcNow - value;
            if (delta.TotalMinutes < 1) return "Just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} hr ago";
            if (delta.TotalDays < 2) return "Yesterday";
            return $"{(int)delta.TotalDays} days ago";
        }

        private static string Initials(string name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0
                ? "NA"
                : string.Concat(parts.Take(2).Select(part => part[0])).ToUpperInvariant();
        }
    }
}
