using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace GameHub.Helpers
{
    public static class TokenHelper
    {
        public static string CreateSecureToken()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        }

        public static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }
    }
}
