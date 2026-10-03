using GameHub.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace GameHub.Helpers
{
    public static class CustomerPasswordHelper
    {
        private static readonly PasswordHasher<Customer> Hasher = new();

        public static string HashPassword(Customer customer, string password)
        {
            return Hasher.HashPassword(customer, password);
        }

        public static bool VerifyPassword(Customer customer, string hashedPassword, string enteredPassword)
        {
            var result = Hasher.VerifyHashedPassword(customer, hashedPassword, enteredPassword);
            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
