using System.Security.Claims;
using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Single source of truth for which roles may reach which ERP modules.
    /// Every controller action, sidebar item and dashboard widget must consult this
    /// class so that permissions stay consistent across the whole application.
    /// </summary>
    public static class RolePermissions
    {
        public static class Modules
        {
            public const string Dashboard = "Dashboard";
            public const string Approvals = "Approvals";
            public const string Refunds = "Refunds";
            public const string Bookings = "Bookings";
            public const string Calendar = "Calendar";
            public const string Memberships = "Memberships";
            public const string Customers = "Customers";
            public const string Crm = "CRM";
            public const string Sales = "Sales";
            public const string Invoices = "Invoices";
            public const string Payments = "Payments";
            public const string MasterData = "MasterData";
            public const string Hr = "HR";
            public const string Reports = "Reports";
            public const string Users = "Users";
            public const string Roles = "Roles";
            public const string Settings = "Settings";
        }

        private static readonly Dictionary<string, UserRole[]> Permissions = new(StringComparer.OrdinalIgnoreCase)
        {
            [Modules.Dashboard] = AllRoles(),
            [Modules.Approvals] = Roles(UserRole.Admin, UserRole.Manager),
            [Modules.Refunds] = Roles(UserRole.Admin, UserRole.Manager, UserRole.Finance),
            [Modules.Bookings] = Roles(UserRole.Admin, UserRole.Receptionist, UserRole.Operations),
            [Modules.Calendar] = Roles(UserRole.Admin, UserRole.Receptionist, UserRole.Operations),
            [Modules.Memberships] = Roles(UserRole.Admin, UserRole.Receptionist),
            [Modules.Customers] = Roles(UserRole.Admin, UserRole.CRM, UserRole.Receptionist),
            [Modules.Crm] = Roles(UserRole.Admin, UserRole.CRM),
            [Modules.Sales] = Roles(UserRole.Admin, UserRole.Sales),
            [Modules.Invoices] = Roles(UserRole.Admin, UserRole.Sales, UserRole.Finance),
            [Modules.Payments] = Roles(UserRole.Admin, UserRole.Finance),
            [Modules.MasterData] = Roles(UserRole.Admin),
            [Modules.Hr] = Roles(UserRole.Admin, UserRole.HR),
            [Modules.Reports] = Roles(UserRole.Admin),
            [Modules.Users] = Roles(UserRole.Admin),
            [Modules.Roles] = Roles(UserRole.Admin),
            [Modules.Settings] = Roles(UserRole.Admin)
        };

        /// <summary>Ordered description of every module, used by the Roles administration screen.</summary>
        public static readonly IReadOnlyList<(string Key, string Name, string Description)> ModuleCatalog = new List<(string, string, string)>
        {
            (Modules.Dashboard, "Dashboard", "Role-specific home dashboard with KPIs and shortcuts for that role only."),
            (Modules.Approvals, "Booking Approvals", "Review and action pending booking approvals."),
            (Modules.Refunds, "Refund Management", "Review and process refund requests."),
            (Modules.Bookings, "Bookings", "Create, edit and manage court bookings."),
            (Modules.Calendar, "Booking Calendar", "Read the day-to-day booking calendar."),
            (Modules.Memberships, "Memberships", "Manage customer memberships and renewals."),
            (Modules.Customers, "Customers", "View and maintain customer records."),
            (Modules.Crm, "CRM", "Leads, opportunities and follow-ups."),
            (Modules.Sales, "Sales", "Quotations and sales orders across the sales cycle."),
            (Modules.Invoices, "Invoices", "Create and review sales invoices."),
            (Modules.Payments, "Payments", "Record and reconcile payments."),
            (Modules.MasterData, "Master Data", "Sports, facilities, courts, pricing and time slots."),
            (Modules.Hr, "HR", "Recruitment, employees, performance, termination and HR reports."),
            (Modules.Reports, "Reports", "Organisation-wide reporting dashboard."),
            (Modules.Users, "Users", "Create and manage ERP user accounts."),
            (Modules.Roles, "Roles", "Review the role and permission matrix."),
            (Modules.Settings, "Configuration", "System configuration and settings.")
        };

        public static bool IsAdmin(UserRole role) => role == UserRole.Admin;

        public static bool IsAdmin(ClaimsPrincipal principal)
            => TryGetCurrentRole(principal, out var role) && IsAdmin(role);

        public static bool CanAccessModule(ClaimsPrincipal principal, string module)
        {
            return TryGetCurrentRole(principal, out var role) && CanAccessModule(role, module);
        }

        public static bool CanAccessModule(UserRole role, string module)
        {
            if (IsAdmin(role))
            {
                return true;
            }

            return Permissions.TryGetValue(module, out var roles) && roles.Contains(role);
        }

        public static IReadOnlyList<string> AllowedModules(UserRole role)
        {
            return Permissions.Where(pair => CanAccessModule(role, pair.Key))
                .Select(pair => pair.Key)
                .ToList();
        }

        public static IReadOnlyList<UserRole> RolesForModule(string module)
        {
            return Permissions.TryGetValue(module, out var roles) ? roles : Array.Empty<UserRole>();
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
