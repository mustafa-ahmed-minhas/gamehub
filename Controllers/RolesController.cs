using GameHub.Data;
using GameHub.Filters;
using GameHub.Helpers;
using GameHub.Models.Enums;
using GameHub.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Controllers
{
    [ModuleAuthorize(RolePermissions.Modules.Roles)]
    [ValidateActiveUser]
    public class RolesController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public RolesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var counts = await _dbContext.Users.AsNoTracking()
                .GroupBy(user => user.Role)
                .Select(group => new { Role = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Role, item => item.Count);

            var model = new RoleMatrixViewModel
            {
                Catalog = RolePermissions.ModuleCatalog.ToList(),
                Roles = Enum.GetValues<UserRole>().Select(role => new RoleMatrixRow
                {
                    Role = role,
                    DisplayName = role.GetDisplayName(),
                    Scope = ScopeFor(role),
                    UserCount = counts.GetValueOrDefault(role),
                    IsSystemAdmin = role == UserRole.Admin,
                    AllowedModules = RolePermissions.AllowedModules(role).ToHashSet(StringComparer.OrdinalIgnoreCase)
                }).ToList()
            };

            return View(model);
        }

        private static string ScopeFor(UserRole role) => role switch
        {
            UserRole.Admin => "Full system access across every module, including Administration and Reports.",
            UserRole.Manager => "Approval-related screens only: booking approvals and refund management.",
            UserRole.Receptionist => "Front-desk modules: customers, bookings, booking calendar and memberships.",
            UserRole.Finance => "Finance modules: payments, invoices and refund management.",
            UserRole.Sales => "The complete sales cycle: quotations, sales orders and invoices.",
            UserRole.CRM => "CRM modules: customers, leads, opportunities and follow-ups.",
            UserRole.HR => "HR modules: recruitment, employees, performance, termination and HR reports.",
            UserRole.Operations => "Operations modules: bookings and the booking calendar.",
            _ => "Limited modules."
        };
    }
}
