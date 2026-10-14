namespace GameHub.Helpers
{
    /// <summary>
    /// Country-aware validation and normalization for national / personal ID numbers.
    /// Pakistan uses the standard 13-digit CNIC (optional hyphens in the input, e.g. 35202-1234567-8).
    /// Other countries fall back to a permissive 4-30 character alphanumeric format.
    /// </summary>
    public static class NationalIdValidator
    {
        public static bool TryNormalize(string? value, string? country, out string normalized, out string errorMessage)
        {
            normalized = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "National ID Number is required.";
                return false;
            }

            var trimmed = value.Trim();

            if (string.Equals(country?.Trim(), "Pakistan", StringComparison.OrdinalIgnoreCase))
            {
                var digits = new string(trimmed.Where(char.IsDigit).ToArray());
                if (digits.Length != 13)
                {
                    errorMessage = "Enter a valid Pakistani CNIC (13 digits), e.g., 35202-1234567-8.";
                    return false;
                }

                normalized = digits;
                return true;
            }

            if (trimmed.Length is < 4 or > 30 || !trimmed.All(c => char.IsLetterOrDigit(c) || c == '-'))
            {
                errorMessage = "Enter a valid national ID number (4-30 letters, digits, or hyphens).";
                return false;
            }

            normalized = trimmed;
            return true;
        }
    }
}