using GameHub.Data;
using GameHub.Helpers;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    /// <summary>
    /// Database-backed duplicate check for job applications. Only the three
    /// comparison fields for a single posting are loaded, then compared after
    /// normalization so equivalent formatting still matches even for rows that
    /// were written before normalization was enforced.
    /// </summary>
    public class JobApplicationDuplicateService : IJobApplicationDuplicateService
    {
        private readonly ApplicationDbContext _dbContext;

        public JobApplicationDuplicateService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> ExistsAsync(
            int jobPostingId,
            string? email,
            string? phone,
            string? nationalIdNumber,
            int? excludeApplicationId = null)
        {
            if (jobPostingId <= 0)
            {
                return false;
            }

            var candidates = await _dbContext.JobApplications
                .AsNoTracking()
                .Where(application => application.JobPostingId == jobPostingId
                    && application.IsActive
                    && (!excludeApplicationId.HasValue || application.Id != excludeApplicationId.Value))
                .Select(application => new
                {
                    application.Email,
                    application.Phone,
                    application.NationalIdNumber
                })
                .ToListAsync();

            if (candidates.Count == 0)
            {
                return false;
            }

            var emailKey = CandidateContactNormalizer.NormalizeEmail(email);
            var phoneKey = CandidateContactNormalizer.PhoneComparisonKey(phone);
            var nationalIdKey = CandidateContactNormalizer.NationalIdComparisonKey(nationalIdNumber);

            return candidates.Any(application =>
                (emailKey.Length > 0 && CandidateContactNormalizer.NormalizeEmail(application.Email) == emailKey)
                || (phoneKey.Length > 0 && CandidateContactNormalizer.PhoneComparisonKey(application.Phone) == phoneKey)
                || (nationalIdKey.Length > 0 && CandidateContactNormalizer.NationalIdComparisonKey(application.NationalIdNumber) == nationalIdKey));
        }
    }
}
