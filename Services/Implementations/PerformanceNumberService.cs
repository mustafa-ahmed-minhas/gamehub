using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    /// <summary>
    /// Generates the sequential, human-readable case reference used by the
    /// Performance &amp; Warnings screen (e.g. PC-1, PC-2). Mirrors the other HR
    /// numbering services so references stay consistent and unique.
    /// </summary>
    public class PerformanceNumberService : IPerformanceNumberService
    {
        private const string Prefix = "PC";

        private readonly ApplicationDbContext _dbContext;

        public PerformanceNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextCaseNumberAsync()
        {
            var latest = await _dbContext.PerformanceCases
                .AsNoTracking()
                .Where(detail => detail.CaseNumber.StartsWith(Prefix + "-"))
                .OrderByDescending(detail => detail.Id)
                .Select(detail => detail.CaseNumber)
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
