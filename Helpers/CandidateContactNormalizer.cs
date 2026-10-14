namespace GameHub.Helpers
{
    /// <summary>
    /// Consistent normalization of candidate contact values so duplicate
    /// detection treats equivalent formatting as the same value (phone spaces,
    /// dashes, brackets and a leading '+' as well as email casing).
    /// Used by the public careers application form and the HR/admin application
    /// screens so both workflows enforce exactly the same rules.
    /// </summary>
    public static class CandidateContactNormalizer
    {
        /// <summary>Canonical stored email: trimmed and lower-cased.</summary>
        public static string NormalizeEmail(string? value)
            => (value ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>Canonical stored phone: an optional leading '+' followed by digits only.</summary>
        public static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var prefix = trimmed.StartsWith('+') ? "+" : string.Empty;
            return prefix + DigitsOnly(trimmed);
        }

        /// <summary>
        /// Phone comparison key: digits only, so "+92 300-1234567", "+923001234567"
        /// and "923001234567" are all treated as the same number.
        /// </summary>
        public static string PhoneComparisonKey(string? value) => DigitsOnly(value);

        /// <summary>National ID comparison key: trimmed and lower-cased.</summary>
        public static string NationalIdComparisonKey(string? value)
            => (value ?? string.Empty).Trim().ToLowerInvariant();

        private static string DigitsOnly(string? value)
            => string.IsNullOrEmpty(value)
                ? string.Empty
                : new string(value.Where(char.IsDigit).ToArray());
    }
}
