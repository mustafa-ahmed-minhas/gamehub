using GameHub.Data;
using GameHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Services.Implementations
{
    public class EmployeeNumberService : IEmployeeNumberService
    {
        private const string Prefix = "EMP";

        private readonly ApplicationDbContext _dbContext;

        public EmployeeNumberService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GenerateNextEmployeeNumberAsync()
        {
            var latest = await _dbContext.Employees
                .AsNoTracking()
                .Where(employee => employee.EmployeeNumber.StartsWith(Prefix + "-"))
                .OrderByDescending(employee => employee.Id)
                .Select(employee => employee.EmployeeNumber)
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