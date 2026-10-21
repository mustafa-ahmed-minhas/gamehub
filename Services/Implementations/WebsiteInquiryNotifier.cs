using System.Linq.Expressions;
using GameHub.Data;
using GameHub.Hubs;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    /// <summary>
    /// Default <see cref="IWebsiteInquiryNotifier"/>. Counts website inquiries in
    /// SQL Server on demand and pushes them over the existing MVC app's SignalR
    /// hub (<see cref="CrmNotificationsHub"/>).
    /// <para>
    /// Every send is wrapped in a try/catch so a transport problem (for example a
    /// missing SignalR client) can never turn a saved inquiry into a failed
    /// request. Broadcasts therefore happen only after the caller has committed
    /// the lead to the database.
    /// </para>
    /// </summary>
    public class WebsiteInquiryNotifier : IWebsiteInquiryNotifier
    {
        /// <summary>SignalR event carrying a new website inquiry (name + counter).</summary>
        public const string NewInquiryEvent = "newWebsiteInquiry";

        /// <summary>SignalR event carrying only the refreshed counter.</summary>
        public const string CountChangedEvent = "websiteInquiryCountChanged";

        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<CrmNotificationsHub> _hubContext;
        private readonly ILogger<WebsiteInquiryNotifier> _logger;

        public WebsiteInquiryNotifier(
            ApplicationDbContext dbContext,
            IHubContext<CrmNotificationsHub> hubContext,
            ILogger<WebsiteInquiryNotifier> logger)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _logger = logger;
        }

        public Task<int> GetUnreviewedCountAsync()
            => _dbContext.Leads.AsNoTracking().CountAsync(AwaitingReview());

        public async Task BroadcastNewInquiryAsync(Lead lead)
        {
            if (lead == null) return;

            var count = await GetUnreviewedCountAsync();
            var payload = new
            {
                leadId = lead.Id,
                leadNumber = lead.LeadNumber,
                customerName = lead.CustomerName,
                reference = lead.PublicReferenceNumber,
                newWebsiteInquiryCount = count
            };

            await BroadcastAsync(NewInquiryEvent, payload);
        }

        public async Task BroadcastCountAsync()
        {
            var count = await GetUnreviewedCountAsync();
            await BroadcastAsync(CountChangedEvent, new { count });
        }

        public async Task SendCountToConnectionAsync(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId)) return;

            var count = await GetUnreviewedCountAsync();
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync(CountChangedEvent, new { count });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Website inquiry counter could not be sent to connection {ConnectionId}.", connectionId);
            }
        }

        /// <summary>
        /// Website inquiries whose workflow status is still <c>New</c>. Once the
        /// status moves off New (via the existing Lead Management actions) the
        /// inquiry counts as reviewed.
        /// </summary>
        private static Expression<Func<Lead, bool>> AwaitingReview() =>
            lead => lead.IsWebsiteInquiry && lead.Status == LeadStatus.New && lead.IsActive;

        private async Task BroadcastAsync(string @event, object payload)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync(@event, payload);
            }
            catch (Exception exception)
            {
                // Real-time delivery is a nice-to-have; the data is already saved.
                _logger.LogWarning(exception, "Website inquiry real-time broadcast failed for event {Event}.", @event);
            }
        }
    }
}
