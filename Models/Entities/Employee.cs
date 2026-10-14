using System.ComponentModel.DataAnnotations;
using GameHub.Models.Enums;

namespace GameHub.Models.Entities
{
    public class Employee
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? ProfileImageUrl { get; set; }

        [Required, MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string NationalIdNumber { get; set; } = string.Empty;

        public DateOnly DateOfBirth { get; set; }

        public Gender Gender { get; set; }

        [Required, MaxLength(80)]
        public string Department { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Designation { get; set; } = string.Empty;

        public EmploymentType EmploymentType { get; set; }

        public EmploymentStatus EmploymentStatus { get; set; }

        public DateOnly JoiningDate { get; set; }

        public int? ReportingManagerId { get; set; }

        public Employee? ReportingManager { get; set; }

        [MaxLength(250)]
        public string? EmploymentAddress { get; set; }

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(30)]
        public string? EmergencyContactNumber { get; set; }

        public decimal? BasicSalary { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public ICollection<PerformanceCase> PerformanceCases { get; set; } = new List<PerformanceCase>();

        public ICollection<TerminationRequest> TerminationRequests { get; set; } = new List<TerminationRequest>();
    }
}