using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class EmployeeListItemViewModel
    {
        public int Id { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public EmploymentType EmploymentType { get; set; }
        public EmploymentStatus EmploymentStatus { get; set; }
        public DateOnly JoiningDate { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ReportingManagerName { get; set; }
    }
}