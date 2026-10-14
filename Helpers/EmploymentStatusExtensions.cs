using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Maps an employee's employment status to the HR badge tone class
    /// used across the Employee Management screens (see hr.css).
    /// </summary>
    public static class EmploymentStatusExtensions
    {
        public static string GetBadgeClass(this EmploymentStatus status) => status switch
        {
            EmploymentStatus.Active => "active",
            EmploymentStatus.OnLeave => "onleave",
            _ => "inactive"
        };
    }
}