namespace GameHub.Models.ViewModels
{
    public class DashboardViewModel
    {
        public string Greeting { get; set; } = "Good Morning";
        public string FirstName { get; set; } = "User";
        public string CurrencySymbol { get; set; } = "Rs";

        public int TodaysBookings { get; set; }
        public decimal TodaysRevenue { get; set; }
        public decimal CourtOccupancyPercent { get; set; }
        public int ActiveCustomers { get; set; }
        public decimal PendingPaymentsTotal { get; set; }
        public int PendingPaymentsCount { get; set; }
        public int CourtsAvailable { get; set; }
        public int CourtsTotal { get; set; }

        public List<DashboardCourtItem> LiveCourts { get; set; } = new();
        public List<DashboardScheduleItem> TodaySchedule { get; set; } = new();

        public string[] RevenueLabels { get; set; } = [];
        public decimal[] RevenueData { get; set; } = [];
        public string[] SportLabels { get; set; } = [];
        public int[] SportData { get; set; } = [];
        public int BookingStatusTotal { get; set; }
        public string[] BookingStatusLabels { get; set; } = [];
        public int[] BookingStatusData { get; set; } = [];

        public List<DashboardTopCourtItem> TopCourts { get; set; } = new();

        public int[][] HeatmapValues { get; set; } = [];
        public string[] HeatmapDayLabels { get; set; } = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        public string[] HeatmapTimeLabels { get; set; } = ["06 AM", "09 AM", "12 PM", "03 PM", "06 PM", "09 PM"];

        public int NewCustomers { get; set; }
        public decimal ReturningCustomerPercent { get; set; }
        public decimal AvgBookingValue { get; set; }
        public int MembershipRenewals { get; set; }

        public List<DashboardActivityItem> RecentActivity { get; set; } = new();
        public List<DashboardAttentionItem> AttentionItems { get; set; } = new();
        public List<DashboardQuickAction> QuickActions { get; set; } = new();
    }

    public class DashboardCourtItem
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Available";
        public string StatusClass { get; set; } = "available";
        public string Detail { get; set; } = "";
    }

    public class DashboardScheduleItem
    {
        public string Time { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string CourtName { get; set; } = "";
        public string SportIcon { get; set; } = "bi-dribbble";
        public string Duration { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string PaymentClass { get; set; } = "";
        public string BookingStatus { get; set; } = "";
        public string StatusClass { get; set; } = "";
    }

    public class DashboardTopCourtItem
    {
        public int Rank { get; set; }
        public string Name { get; set; } = "";
        public string SportIcon { get; set; } = "bi-dribbble";
        public int OccupancyPercent { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DashboardActivityItem
    {
        public string Icon { get; set; } = "bi-info-circle";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public string TimeAgo { get; set; } = "";
        public DateTime SortKey { get; set; }
    }

    public class DashboardAttentionItem
    {
        public string Tone { get; set; } = "warning";
        public string Label { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class DashboardQuickAction
    {
        public string Icon { get; set; } = "bi-plus";
        public string Label { get; set; } = "";
        public string Url { get; set; } = "#";
    }
}
