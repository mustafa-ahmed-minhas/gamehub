namespace GameHub.Models.ViewModels.GameHubTable;

public class GameHubTablePaginationViewModel
{
    public string Summary { get; set; } = string.Empty;
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public Func<int, string>? PageUrl { get; set; }
}
