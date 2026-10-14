using GameHub.Models.Entities;
using GameHub.Models.Enums;

namespace GameHub.Helpers
{
    /// <summary>
    /// Builds the plain-text content of a termination letter from the saved
    /// termination request and employee record. The text is rendered into a PDF
    /// by <c>ISimplePdfGenerator</c>; keeping the composition separate makes the
    /// letter easy to preview or extend, and mirrors OfferLetterComposer.
    /// </summary>
    public static class TerminationLetterComposer
    {
        public const string LetterTitle = "Termination of Employment";

        public static List<string> Compose(
            TerminationRequest request,
            Employee employee,
            string? reportingManagerName,
            string companyName,
            string? companyAddress,
            string companyPhone,
            string companyEmail)
        {
            var employeeName = $"{employee.FirstName} {employee.LastName}".Trim();

            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(companyAddress) ? companyName : $"{companyName} - {companyAddress}",
                string.Empty,
                $"Date: {request.RequestDate:dd MMM yyyy}",
                $"Reference: {request.ReferenceNumber}",
                $"Employee Number: {employee.EmployeeNumber}",
                string.Empty,
                $"Dear {employeeName},",
                string.Empty,
                $"This letter confirms that your employment with {companyName} as " +
                $"{employee.Designation} in the {employee.Department} department is being " +
                $"terminated on the basis of {request.TerminationType.GetDisplayName()}.",
                string.Empty,
                "Termination Summary:",
                $"Reason: {request.Reason}",
                $"Request Date: {request.RequestDate:dd MMM yyyy}",
                $"Proposed Last Working Day: {request.ProposedLastWorkingDay:dd MMM yyyy}",
                $"Notice Period: {ValueOr(request.NoticePeriod, "As per contract or company policy")}",
                $"Reporting Manager: {ValueOr(reportingManagerName, "As recorded with Human Resources")}",
                string.Empty,
                "Details:",
                ValueOr(request.Explanation, "No additional details were provided."),
                string.Empty,
                "Your final settlement and clearance formalities, including the return of " +
                "company assets and handover of duties, will be completed through the " +
                "clearance process before your last working day.",
                string.Empty,
                "Sincerely,",
                "Human Resources",
                companyName,
            };

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
