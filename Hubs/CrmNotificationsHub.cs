using System.Security.Claims;
using GameHub.Data;
using GameHub.Helpers;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Hubs
{
    /// <summary>
    /// Real-time channel for the ERP ("New Website Inquiries").
    /// <para>
    /// The connection is authenticated with the existing admin cookie scheme and
    /// the same CRM module permission used by <c>CRMController</c>
    /// (<see cref="RolePermissions.CanAccessModule"/>), so only signed-in ERP
    /// users who are allowed to view the CRM can receive lead data. The user is
    /// also re-validated against the database (active account, unchanged role),
    /// mirroring <c>ValidateActiveUser</c>, so a revoked account cannot stay
    /// subscribed on a long-lived connection.
    /// </para>
    /// <para>
    /// The role list mirrors the <c>[GameHubAuthorize]</c> roles already declared
    /// on <c>CRMController</c> so the handshake is rejected up front for users
    /// outside the CRM; the runtime check below stays the source of truth.
    /// </para>
    /// </summary>
    [Authorize(Roles = "SuperAdmin,Admin,BookingManager,Receptionist,FinanceManager,Viewer")]
    public class CrmNotificationsHub : Hub
    {
        public const string Route = "/hubs/crm-notifications";

        private const string CrmModule = "CRM";

        private readonly ApplicationDbContext _dbContext;
        private readonly IWebsiteInquiryNotifier _notifier;
        private readonly ILogger<CrmNotificationsHub> _logger;

        public CrmNotificationsHub(ApplicationDbContext dbContext, IWebsiteInquiryNotifier notifier, ILogger<CrmNotificationsHub> logger)
        {
            _dbContext = dbContext;
            _notifier = notifier;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            if (!await IsPermittedAsync())
            {
                _logger.LogWarning("Rejected CRM real-time connection for {User}.", Context.User?.Identity?.Name ?? "unknown");
                Context.Abort();
                return;
            }

            // Sync the freshly connected tab with the authoritative database count.
            await _notifier.SendCountToConnectionAsync(Context.ConnectionId);
        }

        private async Task<bool> IsPermittedAsync()
        {
            var user = Context.User;
            if (user?.Identity?.IsAuthenticated != true) return false;
            if (!RolePermissions.CanAccessModule(user, CrmModule)) return false;

            if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return false;
            if (!Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), out var claimRole)) return false;

            var account = await _dbContext.Users.AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new { item.IsActive, item.Role })
                .FirstOrDefaultAsync();

            return account != null && account.IsActive && account.Role == claimRole;
        }
    }
}
