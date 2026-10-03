using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class BookingNumberService : IBookingNumberService
    {
        private readonly ApplicationDbContext _dbContext;

        public BookingNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextNumberAsync()
        {
            var prefix = await _dbContext.SystemSettings.AsNoTracking()
                .Select(settings => settings.BookingPrefix)
                .FirstOrDefaultAsync();

            prefix = string.IsNullOrWhiteSpace(prefix) ? "BKG" : prefix.Trim().ToUpperInvariant();
            var next = await _dbContext.Bookings.CountAsync() + 1;
            string number;
            do
            {
                number = $"{prefix}-{next:000000}";
                next++;
            }
            while (await _dbContext.Bookings.AnyAsync(booking => booking.BookingNumber == number));

            return number;
        }
    }
}
