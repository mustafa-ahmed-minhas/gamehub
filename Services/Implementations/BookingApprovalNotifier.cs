using GameHub.Data;
using GameHub.Hubs;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using GameHub.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class BookingApprovalNotifier : IBookingApprovalNotifier
    {
        public const string NewPendingEvent = "newBookingApproval";
        public const string CountChangedEvent = "bookingApprovalCountChanged";

        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<CrmNotificationsHub> _hubContext;
        private readonly ILogger<BookingApprovalNotifier> _logger;

        public BookingApprovalNotifier(ApplicationDbContext dbContext, IHubContext<CrmNotificationsHub> hubContext, ILogger<BookingApprovalNotifier> logger)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _logger = logger;
        }

        public Task<int> GetPendingCountAsync() =>
            _dbContext.Bookings.AsNoTracking().CountAsync(b => b.IsActive && b.Status == BookingStatus.PendingApproval);

        public async Task BroadcastNewPendingAsync(Booking booking)
        {
            if (booking == null) return;
            var count = await GetPendingCountAsync();
            var payload = new
            {
                bookingId = booking.Id,
                bookingNumber = booking.BookingNumber,
                pendingCount = count
            };
            await BroadcastAsync(NewPendingEvent, payload);
        }

        public async Task BroadcastCountAsync()
        {
            var count = await GetPendingCountAsync();
            await BroadcastAsync(CountChangedEvent, new { count });
        }

        public async Task SendCountToConnectionAsync(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId)) return;
            var count = await GetPendingCountAsync();
            try
            {
                await _hubContext.Clients.Client(connectionId).SendAsync(CountChangedEvent, new { count });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Booking approval count could not be sent to connection {ConnectionId}.", connectionId);
            }
        }

        private async Task BroadcastAsync(string @event, object payload)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync(@event, payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Booking approval real-time broadcast failed for event {Event}.", @event);
            }
        }
    }
}