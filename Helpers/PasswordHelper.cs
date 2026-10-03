using GameHub.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace GameHub.Helpers
{
    public static class PasswordHelper
    {
        private static readonly PasswordHasher<User> Hasher = new();

        public static string HashPassword(User user, string password)
        {
            return Hasher.HashPassword(user, password);
        }

        public static bool VerifyPassword(User user, string hashedPassword, string enteredPassword)
        {
            var result = Hasher.VerifyHashedPassword(user, hashedPassword, enteredPassword);
            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
