namespace GameHub.Models.ViewModels
{
    public class HRDashboardViewModel
    {
        public string Greeting { get; set; } = "Good Morning";
        public string FirstName { get; set; } = "User";

        // Summary cards
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int OpenPositions { get; set; }
        public int PendingApplications { get; set; }
        public int UpcomingInterviews { get; set; }
        public int PendingHrActions { get; set; }

        public List<HRRecruitmentActivityItem> RecentRecruitment { get; set; } = new();
        public List<HRTaskItem> PendingTasks { get; set; } = new();
    }

    public class HRRecruitmentActivityItem
    {
        public string Candidate { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Position { get; set; } = "";
        public string Stage { get; set; } = "";
        public string StageClass { get; set; } = "new";
        public string Applied { get; set; } = "";
        public string LastUpdate { get; set; } = "";
    }

    public class HRTaskItem
    {
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string Owner { get; set; } = "";
        public string Due { get; set; } = "";
        public string Priority { get; set; } = "";
        public string PriorityClass { get; set; } = "medium";
        public string Status { get; set; } = "";
        public string StatusClass { get; set; } = "progress";
    }
}