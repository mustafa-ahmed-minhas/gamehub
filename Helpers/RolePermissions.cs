using System.Security.Claims;
using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    public static class RolePermissions
    {
        private static readonly Dictionary<string, UserRole[]> Permissions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = AllRoles(),
            ["ArenaSetup"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.CourtManager, UserRole.Viewer),
            ["Bookings"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.CourtManager, UserRole.FinanceManager, UserRole.Viewer),
            ["Calendar"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.CourtManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer),
            ["Customers"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer),
            ["Memberships"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer),
            ["Feedback"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Viewer),
            ["Payments"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.FinanceManager, UserRole.Receptionist, UserRole.Viewer),
            ["Expenses"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.FinanceManager, UserRole.Viewer),
            ["Invoices"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.FinanceManager, UserRole.Receptionist, UserRole.Viewer),
            ["Staff"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer),
            ["Attendance"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer),
            ["Shifts"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.StaffManager, UserRole.Viewer),
            ["Equipment"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.CourtManager, UserRole.StaffManager, UserRole.Viewer),
            ["Maintenance"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.CourtManager, UserRole.StaffManager, UserRole.Viewer),
            ["Reports"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.FinanceManager, UserRole.BookingManager, UserRole.CourtManager, UserRole.StaffManager, UserRole.Viewer),
            ["Settings"] = Roles(UserRole.SuperAdmin, UserRole.Admin),
            ["Users"] = Roles(UserRole.SuperAdmin),
            ["CRM"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer),
            ["Sales"] = Roles(UserRole.SuperAdmin, UserRole.Admin, UserRole.BookingManager, UserRole.Receptionist, UserRole.FinanceManager, UserRole.Viewer)
        };

        public static bool CanAccessModule(ClaimsPrincipal principal, string module)
        {
            return TryGetCurrentRole(principal, out var role) && CanAccessModule(role, module);
        }

        public static bool CanAccessModule(UserRole role, string module)
        {
            return Permissions.TryGetValue(module, out var roles) && roles.Contains(role);
        }

        public static bool TryGetCurrentRole(ClaimsPrincipal principal, out UserRole role)
        {
            var roleValue = principal.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse(roleValue, out role);
        }

        public static string GetCurrentRoleDisplay(ClaimsPrincipal principal)
        {
            return TryGetCurrentRole(principal, out var role) ? role.GetDisplayName() : "User";
        }

        private static UserRole[] Roles(params UserRole[] roles) => roles;

        private static UserRole[] AllRoles() => Enum.GetValues<UserRole>();
    }
}
