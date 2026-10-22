using GameHub.Models.Entities;

namespace GameHub.Services.Interfaces
{
    public interface IBookingApprovalNotifier
    {
        Task<int> GetPendingCountAsync();
        Task BroadcastNewPendingAsync(Booking booking);
        Task BroadcastCountAsync();
        Task SendCountToConnectionAsync(string connectionId);
    }
}