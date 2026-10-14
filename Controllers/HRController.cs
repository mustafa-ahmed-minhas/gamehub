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
    [GameHubAuthorize(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer)]
    [ValidateActiveUser]
    public class HRController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public HRController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Screen 1 — HR Dashboard. Every figure is sourced from live Employee,
        // Job Posting, Job Application, and Interview records; no placeholder data.
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
            var weekAhead = today.AddDays(7);

            var awaitingReview = await _dbContext.JobApplications.AsNoTracking()
                .CountAsync(application => application.IsActive
                    && (application.Status == JobApplicationStatus.Submitted
                        || application.Status == JobApplicationStatus.Screening));
            var shortlistedWithoutInterview = await _dbContext.JobApplications.AsNoTracking()
                .CountAsync(application => application.IsActive
                    && application.Status == JobApplicationStatus.Shortlisted
                    && !application.Interviews.Any(interview => interview.IsActive));
            var closingSoon = await _dbContext.JobPostings.AsNoTracking()
                .CountAsync(posting => posting.IsActive
                    && posting.Status == JobPostingStatus.Open
                    && posting.ApplicationDeadline >= today
                    && posting.ApplicationDeadline <= weekAhead);
            var overdueInterviews = await _dbContext.Interviews.AsNoTracking()
                .CountAsync(interview => interview.IsActive
                    && interview.Status == InterviewStatus.Scheduled
                    && interview.InterviewDate < today);

            var model = new HRDashboardViewModel
            {
                Greeting = greeting,
                FirstName = firstName,
                TotalEmployees = await _dbContext.Employees.AsNoTracking()
                    .CountAsync(employee => employee.IsActive),
                ActiveEmployees = await _dbContext.Employees.AsNoTracking()
                    .CountAsync(employee => employee.IsActive
                        && employee.EmploymentStatus == EmploymentStatus.Active),
                OpenPositions = await _dbContext.JobPostings.AsNoTracking()
                    .CountAsync(posting => posting.IsActive
                        && posting.Status == JobPostingStatus.Open
                        && posting.ApplicationDeadline >= today),
                PendingApplications = awaitingReview,
                UpcomingInterviews = await _dbContext.Interviews.AsNoTracking()
                    .CountAsync(interview => interview.IsActive
                        && interview.Status == InterviewStatus.Scheduled
                        && interview.InterviewDate >= today),
                PendingHrActions = awaitingReview + shortlistedWithoutInterview + closingSoon + overdueInterviews
            };

            model.RecentRecruitment = await BuildRecentRecruitmentAsync();
            model.PendingTasks = BuildPendingTasks(awaitingReview, shortlistedWithoutInterview, closingSoon, overdueInterviews);

            return model;
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
                    "Action Needed", "action"));
            }

            if (shortlistedWithoutInterview > 0)
            {
                tasks.Add(TaskItem(
                    $"{shortlistedWithoutInterview} shortlisted candidate{(shortlistedWithoutInterview == 1 ? "" : "s")} without an interview",
                    "Recruitment", "Recruitment Team", "This week",
                    "High", "high", "Action Needed", "action"));
            }

            if (overdueInterviews > 0)
            {
                tasks.Add(TaskItem(
                    $"{overdueInterviews} past interview{(overdueInterviews == 1 ? "" : "s")} awaiting outcome",
                    "Interview", "Recruitment Team", "Today",
                    "High", "high", "Action Needed", "action"));
            }

            if (closingSoon > 0)
            {
                tasks.Add(TaskItem(
                    $"{closingSoon} job posting{(closingSoon == 1 ? "" : "s")} closing within 7 days",
                    "Recruitment", "Recruitment Team", "This week",
                    "Medium", "medium", "In Progress", "progress"));
            }

            return tasks;
        }

        private static HRTaskItem TaskItem(
            string title, string category, string owner, string due,
            string priority, string priorityClass, string status, string statusClass) => new()
        {
            Title = title,
            Category = category,
            Owner = owner,
            Due = due,
            Priority = priority,
            PriorityClass = priorityClass,
            Status = status,
            StatusClass = statusClass
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
