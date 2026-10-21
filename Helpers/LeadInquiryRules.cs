using System.Text.RegularExpressions;

namespace GameHub.Helpers
{
    /// <summary>
    /// Canonical validation and normalization rules for CRM leads/inquiries.
    /// Shared by the ERP manual lead-creation workflow (CRMController) and the
    /// public website inquiry endpoint (InquiryController) so both enforce
    /// exactly the same business rules and store values in the same format.
    /// Extracted verbatim from the original private members of CRMController -
    /// this is a pure refactor with no behavior change for the ERP.
    /// </summary>
    public static class LeadInquiryRules
    {
        public const string PhoneFormatMessage = "Enter an 11-digit phone number starting with 0 (e.g. 03001234567).";
        public const string EmailFormatMessage = "Enter a valid email address ending in .com.";
        public const string ContactRequiredMessage = "At least one contact method is required.";
        public const string PreferredDatePastMessage = "Preferred date cannot be in the past.";

        private static readonly Regex PhonePattern = new(@"^0\d{10}$", RegexOptions.Compiled);
        private static readonly Regex ComEmailPattern = new(@"^[^\s@]+@[^\s@]+\.com$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Validates the Pakistan mobile format the ERP requires: 11 digits starting with 0.</summary>
        public static bool HasValidPakPhone(string? value) => !string.IsNullOrWhiteSpace(value) && PhonePattern.IsMatch(value);

        /// <summary>Validates the email format the ERP requires: a normal address ending in .com.</summary>
        public static bool HasValidComEmail(string? value) => !string.IsNullOrWhiteSpace(value) && ComEmailPattern.IsMatch(value);

        /// <summary>A lead is contactable if it has at least one of email, phone or WhatsApp.</summary>
        public static bool HasContact(string? email, string? phone, string? whatsAppNumber)
            => !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(whatsAppNumber);

        /// <summary>Trims a single-line value; returns null when blank so optional fields stay null.</summary>
        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>Canonical stored email: trimmed and lower-cased.</summary>
        public static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

        /// <summary>Canonical stored phone: an optional leading '+' followed by digits only.</summary>
        public static string? NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            var prefix = trimmed.StartsWith('+') ? "+" : string.Empty;
            return prefix + new string(trimmed.Where(char.IsDigit).ToArray());
        }

        /// <summary>
        /// Sanitizes free-text input for storage: strips disallowed control
        /// characters, trims, and caps at the column's max length. Newlines and
        /// tabs are preserved for multi-line fields. Returns null when nothing
        /// meaningful remains. This is defense-in-depth on top of the ERP's
        /// trim-only Clean() and does not change how normal input is stored.
        /// </summary>
        public static string? SanitizeFreeText(string? value, bool allowMultiline, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var builder = new System.Text.StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsControl(c))
                {
                    if (allowMultiline && (c == '\n' || c == '\r' || c == '\t')) builder.Append(c);
                    continue;
                }
                builder.Append(c);
            }
            var cleaned = builder.ToString().Trim();
            if (cleaned.Length == 0) return null;
            return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
        }
    }
}
