using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class EmployeeDetailsViewModel
    {
        public int Id { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string NationalIdNumber { get; set; } = string.Empty;
        public DateOnly DateOfBirth { get; set; }
        public int? CalculatedAge { get; set; }
        public Gender Gender { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public EmploymentType EmploymentType { get; set; }
        public EmploymentStatus EmploymentStatus { get; set; }
        public DateOnly JoiningDate { get; set; }
        public int? ReportingManagerId { get; set; }
        public string? ReportingManagerName { get; set; }
        public string? EmploymentAddress { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactNumber { get; set; }
        public decimal? BasicSalary { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedByName { get; set; }
        public bool CanManage { get; set; }
    }
}