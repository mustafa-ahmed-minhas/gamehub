namespace GameHub.Models.ViewModels.GameHubTable;

public class GameHubTableEmptyStateViewModel
{
    public string IconClass { get; set; } = "bi bi-inbox";
    public string Title { get; set; } = "No records found";
    public string Description { get; set; } = "Try adjusting your filters.";
    public string? ActionText { get; set; }
    public string? ActionUrl { get; set; }
}
