using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class UserIndexViewModel
    {
        public IReadOnlyList<UserListItemViewModel> Users { get; set; } = Array.Empty<UserListItemViewModel>();
        public string? Search { get; set; }
        public UserRole? Role { get; set; }
        public string? Status { get; set; }
        public string SortBy { get; set; } = "newest";
        public string SortDirection { get; set; } = "desc";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int AdminLevelUsers { get; set; }
    }
}
