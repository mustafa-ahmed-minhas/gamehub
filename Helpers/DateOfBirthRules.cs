namespace GameHub.Helpers
{
    /// <summary>
    /// Canonical Date-of-Birth validation shared by every module that collects a
    /// date of birth: public customer signup, the customer master, the customer
    /// self-service profile, ERP users, HR employees and job applicants.
    ///
    /// The product rule is a minimum age of 16. Modules with an already stricter
    /// requirement (HR employees and job applicants must be 18 or older) pass
    /// their own minimum age and message, so their existing, tighter business
    /// rule is preserved while the shared 16+ floor still applies everywhere.
    /// </summary>
    public static class DateOfBirthRules
    {
        /// <summary>The global minimum age enforced everywhere a date of birth is collected.</summary>
        public const int MinimumAge = 16;

        /// <summary>The oldest plausible age; later values are treated as invalid dates.</summary>
        public const int MaximumAgeYears = 120;

        /// <summary>The exact message every under-16 rejection must use.</summary>
        public const string UnderageMessage = "You are underage. You must be at least 16 years old to register.";

        public const string FutureMessage = "Date of birth cannot be in the future.";

        public const string InvalidMessage = "Please enter a valid date of birth.";

        /// <summary>The current UTC date, used as the reference for every age calculation.</summary>
        public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

        /// <summary>The latest date of birth that still satisfies <paramref name="minimumAge"/> years.</summary>
        public static DateOnly LatestAllowedDate(int minimumAge = MinimumAge) => Today.AddYears(-minimumAge);

        /// <summary>The earliest plausible date of birth, used as the invalid-date lower bound.</summary>
        public static DateOnly EarliestAllowedDate => Today.AddYears(-MaximumAgeYears);

        /// <summary>Calculates the completed years between a date of birth and <paramref name="today"/>.</summary>
        public static int CalculateAge(DateOnly dateOfBirth, DateOnly? today = null)
        {
            var reference = today ?? Today;
            var age = reference.Year - dateOfBirth.Year;
            if (dateOfBirth > reference.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        /// <summary>True when the date of birth is on or before the '<paramref name="years"/> years ago' cutoff.</summary>
        public static bool IsAtLeastAge(DateOnly? dateOfBirth, int years, DateOnly? today = null)
            => dateOfBirth.HasValue && CalculateAge(dateOfBirth.Value, today) >= years;

        /// <summary>
        /// Validates a date of birth for the forms and API endpoints that accept one.
        /// Rejects future and impossible dates, and anyone younger than the shared
        /// 16-year floor. Returns <c>null</c> when the value is acceptable (or not
        /// supplied at all - optional fields rely on their own required rules).
        /// </summary>
        /// <param name="dateOfBirth">The value to validate.</param>
        /// <param name="minimumAge">The strictest minimum age for this module (defaults to 16).</param>
        /// <param name="minimumAgeMessage">
        /// Message used when the value passes the 16-year floor but is below a
        /// stricter <paramref name="minimumAge"/> (e.g. the HR 18+ rule).
        /// </param>
        /// <param name="today">Reference date; defaults to the current UTC date.</param>
        public static string? Validate(
            DateOnly? dateOfBirth,
            int minimumAge = MinimumAge,
            string? minimumAgeMessage = null,
            DateOnly? today = null)
        {
            if (!dateOfBirth.HasValue)
            {
                return null;
            }

            var reference = today ?? Today;
            var value = dateOfBirth.Value;

            if (value > reference)
            {
                return FutureMessage;
            }

            if (value < reference.AddYears(-MaximumAgeYears))
            {
                return InvalidMessage;
            }

            var age = CalculateAge(value, reference);
            if (age < MinimumAge)
            {
                return UnderageMessage;
            }

            if (age < minimumAge)
            {
                return minimumAgeMessage ?? $"You must be at least {minimumAge} years old.";
            }

            return null;
        }
    }
}
