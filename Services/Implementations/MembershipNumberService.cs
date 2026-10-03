using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class MembershipNumberService : IMembershipNumberService
    {
        private readonly ApplicationDbContext _dbContext;

        public MembershipNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextNumberAsync()
        {
            const string prefix = "MEM";
            var latest = await _dbContext.CustomerMemberships
                .AsNoTracking()
                .Where(item => item.MembershipNumber.StartsWith(prefix + "-"))
                .OrderByDescending(item => item.Id)
                .Select(item => item.MembershipNumber)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(latest) && int.TryParse(latest.Split('-').LastOrDefault(), out var parsed))
            {
                next = parsed + 1;
            }

            string number;
            do
            {
                number = $"{prefix}-{next:000000}";
                next++;
            }
            while (await _dbContext.CustomerMemberships.AnyAsync(item => item.MembershipNumber == number));

            return number;
        }
    }
}
