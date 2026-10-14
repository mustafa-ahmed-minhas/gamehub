using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    /// <summary>
    /// Generates the sequential, human-readable reference used by the
    /// Termination &amp; Clearance screen (e.g. TRM-1, TRM-2). Mirrors the other
    /// HR numbering services so references stay consistent and unique.
    /// </summary>
    public class TerminationNumberService : ITerminationNumberService
    {
        private const string Prefix = "TRM";

        private readonly ApplicationDbContext _dbContext;

        public TerminationNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextReferenceAsync()
        {
            var latest = await _dbContext.TerminationRequests
                .AsNoTracking()
                .Where(request => request.ReferenceNumber.StartsWith(Prefix + "-"))
                .OrderByDescending(request => request.Id)
                .Select(request => request.ReferenceNumber)
                .FirstOrDefaultAsync();

            var nextNumber = 1;
            if (!string.IsNullOrWhiteSpace(latest))
            {
                var numericPart = latest.Split('-').LastOrDefault();
                if (int.TryParse(numericPart, out var parsed))
                {
                    nextNumber = parsed + 1;
                }
            }

            return $"{Prefix}-{nextNumber}";
        }
    }
}
