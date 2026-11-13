using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    /// <summary>
    /// Data contract for the role-specific dashboards. Every property is populated
    /// from the role's own modules only, so a dashboard can never leak another
    /// department's data.
    /// </summary>
    public class RoleDashboardViewModel
    {
        public UserRole Role { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Greeting { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = "Rs";
        public string Headline { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string PanelTitle { get; set; } = string.Empty;
        public string PanelSubtitle { get; set; } = string.Empty;
        public string EmptyState { get; set; } = "Nothing needs your attention right now.";
        public string PanelLinkLabel { get; set; } = string.Empty;
        public string PanelController { get; set; } = string.Empty;
        public string PanelAction { get; set; } = "Index";
        public List<RoleDashboardKpi> Kpis { get; set; } = new();
        public List<RoleDashboardRow> Rows { get; set; } = new();
        public List<RoleDashboardAction> QuickActions { get; set; } = new();
    }

    public class RoleDashboardKpi
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-activity";
        public string Tone { get; set; } = "info";
    }

    public class RoleDashboardRow
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "pending";
        public string Icon { get; set; } = "bi-dot";
    }

    public class RoleDashboardAction
    {
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = "Index";
        public string Icon { get; set; } = "bi-arrow-up-right";
    }
}
