using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class CustomerCodeService : ICustomerCodeService
    {
        private readonly ApplicationDbContext _dbContext;

        public CustomerCodeService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextCodeAsync()
        {
            const string prefix = "CUS";
            var latestCode = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.CustomerCode.StartsWith(prefix + "-"))
                .OrderByDescending(customer => customer.Id)
                .Select(customer => customer.CustomerCode)
                .FirstOrDefaultAsync();

            var nextNumber = 1;
            if (!string.IsNullOrWhiteSpace(latestCode))
            {
                var numericPart = latestCode.Split('-').LastOrDefault();
                if (int.TryParse(numericPart, out var parsed))
                {
                    nextNumber = parsed + 1;
                }
            }

            string code;
            do
            {
                code = $"{prefix}-{nextNumber:000000}";
                nextNumber++;
            }
            while (await _dbContext.Customers.AnyAsync(customer => customer.CustomerCode == code));

            return code;
        }
    }
}
