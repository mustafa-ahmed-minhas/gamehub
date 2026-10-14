using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Builds the plain-text content of a job offer letter from the saved
    /// offer, application, and posting records. The text is rendered into a
    /// PDF by <c>ISimplePdfGenerator</c>; keeping the composition separate
    /// makes the letter easy to preview or extend.
    /// </summary>
    public static class OfferLetterComposer
    {
        public const string LetterTitle = "Offer of Employment";

        public static List<string> Compose(
            JobOffer offer,
            JobApplication application,
            JobPosting? posting,
            string? reportingManagerName,
            string companyName,
            string? companyAddress,
            string companyPhone,
            string companyEmail)
        {
            var candidateName = $"{application.FirstName} {application.LastName}".Trim();
            var frequency = offer.SalaryFrequency switch
            {
                SalaryFrequency.Annual => "annum",
                SalaryFrequency.Hourly => "hour",
                _ => "month"
            };

            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(companyAddress) ? companyName : $"{companyName} - {companyAddress}",
                string.Empty,
                $"Date: {offer.OfferDate:dd MMM yyyy}",
                $"Offer Reference: {offer.ReferenceNumber}",
                $"Application Reference: {application.ApplicationNumber}",
            };

            if (posting is not null)
            {
                lines.Add($"Position Advertised: {posting.JobTitle} ({posting.ReferenceNumber})");
            }

            lines.AddRange(new[]
            {
                string.Empty,
                $"Dear {candidateName},",
                string.Empty,
                $"We are pleased to offer you the position of {offer.Designation} in the " +
                $"{offer.Department} department at {companyName}, on a " +
                $"{offer.EmploymentType.GetDisplayName()} basis.",
                string.Empty,
                $"Compensation: {offer.Currency} {offer.OfferedSalary:N2} per {frequency}",
                $"Work Location: {offer.WorkLocation}",
                $"Joining Date: {offer.JoiningDate:dd MMM yyyy}",
                $"Offer Valid Until: {offer.ExpiryDate:dd MMM yyyy}",
                $"Reporting Manager: {ValueOr(reportingManagerName, "To be assigned")}",
                $"Probation Period: {ValueOr(offer.ProbationPeriodMonths is { } months && months > 0 ? $"{months} month(s)" : null, "Not applicable")}",
                string.Empty,
                "Benefits:",
                ValueOr(offer.Benefits, "As per company policy."),
                string.Empty,
                "Terms & Conditions:",
                ValueOr(offer.Terms, "Standard employment terms and conditions apply."),
                string.Empty,
                "Please confirm your acceptance by signing and returning a copy of this " +
                "letter before the expiry date shown above. This offer is contingent on " +
                "satisfactory verification of your documents and references.",
                string.Empty,
                "We look forward to welcoming you to the team.",
                string.Empty,
                "Sincerely,",
                "Human Resources",
                companyName,
            });

            var contact = string.Join(" | ", new[] { companyPhone, companyEmail }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(contact))
            {
                lines.Add(contact);
            }

            return lines;
        }

        private static string ValueOr(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
