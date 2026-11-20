using GameHub.Helpers;
using GameHub.Models.Entities;
using GameHub.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Data
{
    /// <summary>
    /// Ensures the single built-in Administrator account exists. Runs on every
    /// startup in all environments; it never modifies an existing account and
    /// never inserts any other users.
    /// </summary>
    public static class AdminAccountInitializer
    {
        public const string AdminEmail = "admin@gamehub.com";
        public const string AdminPassword = "GameHub@123";

        public static async Task EnsureAdminAccountAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var exists = await dbContext.Users.AnyAsync(user => user.Email.ToLower() == AdminEmail);
            if (exists)
            {
                return;
            }

            var admin = new User
            {
                FirstName = "GameHub",
                LastName = "Administrator",
                Email = AdminEmail,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            admin.PasswordHash = PasswordHelper.HashPassword(admin, AdminPassword);

            dbContext.Users.Add(admin);
            await dbContext.SaveChangesAsync();
        }
    }
}
