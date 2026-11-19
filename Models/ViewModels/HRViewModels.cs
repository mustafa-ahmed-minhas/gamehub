namespace GameHub.Models.ViewModels
{
    public class HRDashboardViewModel
    {
        public string Greeting { get; set; } = "Good Morning";
        public string FirstName { get; set; } = "User";
        public DateTime RefreshedAt { get; set; }

        // Headline KPI strip
        public IReadOnlyList<HRDashboardKpiViewModel> Kpis { get; set; } = Array.Empty<HRDashboardKpiViewModel>();

        // Workforce and recruitment analytics
        public IReadOnlyList<HRDashboardChartViewModel> Charts { get; set; } = Array.Empty<HRDashboardChartViewModel>();
        public IReadOnlyList<HRPipelineStageViewModel> Pipeline { get; set; } = Array.Empty<HRPipelineStageViewModel>();
        public int PipelineTotal { get; set; }
        public IReadOnlyList<ReportInsightViewModel> Insights { get; set; } = Array.Empty<ReportInsightViewModel>();

        public int TotalApplications { get; set; }
        public List<HRRecruitmentActivityItem> RecentRecruitment { get; set; } = new();
        public List<HRTaskItem> PendingTasks { get; set; } = new();
    }

    /// <summary>A headline figure with a real, data-derived comparison drawn from live records.</summary>
    public class HRDashboardKpiViewModel
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string Helper { get; set; } = "";
        public string Icon { get; set; } = "bi-bar-chart";
        public string Tone { get; set; } = "";

        /// <summary>Short comparison text shown in the trend pill (empty hides the pill).</summary>
        public string Trend { get; set; } = "";
        public string TrendTone { get; set; } = "neutral";
        public string TrendIcon { get; set; } = "bi-dash";
    }

    /// <summary>
    /// Chart payload for the dashboard. Supports a doughnut with server-assigned slice
    /// colours, or a bar/line chart with one or more series. Every value is aggregated
    /// from live HR records by <c>HRController</c>.
    /// </summary>
    public class HRDashboardChartViewModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Type { get; set; } = "bar";
        public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<HRDashboardChartSeries> Series { get; set; } = Array.Empty<HRDashboardChartSeries>();
        public IReadOnlyList<string> Colors { get; set; } = Array.Empty<string>();
        public string? CenterValue { get; set; }
        public string? CenterLabel { get; set; }
        public bool Horizontal { get; set; }
        public bool ShowLegend { get; set; }
        public bool HasData => Series.Any(series => series.Values.Any(value => value > 0));
    }

    public class HRDashboardChartSeries
    {
        public string Label { get; set; } = "";
        public string Type { get; set; } = "";
        public IReadOnlyList<decimal> Values { get; set; } = Array.Empty<decimal>();
        public string? Color { get; set; }
        public bool Fill { get; set; }
    }

    /// <summary>One stage of the recruitment pipeline with its live count and share.</summary>
    public class HRPipelineStageViewModel
    {
        public string Label { get; set; } = "";
        public string Tone { get; set; } = "new";
        public int Count { get; set; }
        public double Percent { get; set; }
        public string Color { get; set; } = "#d6a652";
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
        public string Icon { get; set; } = "bi-check2-circle";
    }
}
