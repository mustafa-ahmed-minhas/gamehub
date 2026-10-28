using System.Text.RegularExpressions;

namespace GameHub.Helpers
{
    /// <summary>
    /// Field rules shared by every HR form (employees, recruitment, offers,
    /// performance, termination, and the public careers pages) so the same
    /// value is accepted or rejected identically everywhere.
    /// </summary>
    public static class HrValidationRules
    {
        /// <summary>
        /// Person names: letters grouped by single spaces, hyphens, or
        /// apostrophes. Rejects digits, symbols, leading/trailing separators,
        /// and repeated separators.
        /// </summary>
        public const string PersonNamePattern = @"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$";

        public const string PersonNameMessage = "Use letters, spaces, hyphens, and apostrophes only.";

        /// <summary>A syntactically valid address that ends with the .com domain.</summary>
        public const string EmailPattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.com$";

        public const string EmailMessage = "Enter a valid email address ending with .com (e.g. name@example.com).";

        public const string PhonePattern = @"^0\d{10}$";

        public const string PhoneMessage = "Phone number must start with 0 and contain exactly 11 digits (e.g., 03001234567).";

        public const string NationalIdPattern = @"^\d{13}$";

        public const string NationalIdMessage = "National ID / CNIC must be exactly 13 numeric digits (e.g., 3520212345678).";

        private static readonly Regex EmailRegex = new(
            EmailPattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>True when the value is a valid address ending with .com.</summary>
        public static bool IsComEmail(string? value)
            => !string.IsNullOrWhiteSpace(value) && EmailRegex.IsMatch(value.Trim());
    }
}
