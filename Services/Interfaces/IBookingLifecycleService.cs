using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Services.Interfaces
{
    public interface IBookingLifecycleService
    {
        Task<int> SynchronizeAsync(int customerId);

        Task<int> SynchronizeAllAsync();
    }

    public class BookingLifecycleResult
    {
        public int CompletedCount { get; set; }
        public int NoShowCount { get; set; }
        public int Total => CompletedCount + NoShowCount;
    }
}
