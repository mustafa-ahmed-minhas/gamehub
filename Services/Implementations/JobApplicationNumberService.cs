using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class JobApplicationNumberService : IJobApplicationNumberService
    {
        private const string Prefix = "APP";

        private readonly ApplicationDbContext _dbContext;

        public JobApplicationNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextReferenceAsync()
        {
            var latest = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(application => application.ApplicationNumber.StartsWith(Prefix + "-"))
                .OrderByDescending(application => application.Id)
                .Select(application => application.ApplicationNumber)
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