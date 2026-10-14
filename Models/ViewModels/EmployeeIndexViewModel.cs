using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class EmployeeIndexViewModel
    {
        public IReadOnlyList<EmployeeListItemViewModel> Employees { get; set; } = Array.Empty<EmployeeListItemViewModel>();
        public string? Search { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }
        public EmploymentStatus? EmploymentStatus { get; set; }
        public EmploymentType? EmploymentType { get; set; }
        public string SortBy { get; set; } = "newest";
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int OnLeaveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public List<string> Departments { get; set; } = new();
        public List<string> Designations { get; set; } = new();
        public bool CanManage { get; set; }
    }
}