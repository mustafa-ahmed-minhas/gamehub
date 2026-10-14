using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class JobPostingNumberService : IJobPostingNumberService
    {
        private const string Prefix = "JOB";

        private readonly ApplicationDbContext _dbContext;

        public JobPostingNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextReferenceAsync()
        {
            var latest = await _dbContext.JobPostings
                .AsNoTracking()
                .Where(posting => posting.ReferenceNumber.StartsWith(Prefix + "-"))
                .OrderByDescending(posting => posting.Id)
                .Select(posting => posting.ReferenceNumber)
                .FirstOrDefaultAsync();

            var nextNumber = 1;
            if (!string.IsNullOrWhiteSpace(latest))
            {
                var numericPart = latest.Split('-').LastOrDefault();
                if (int.TryParse(numericPart, out var lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{Prefix}-{nextNumber:000000}";
        }
    }
}