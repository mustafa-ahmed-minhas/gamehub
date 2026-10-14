namespace GameHub.Services.Interfaces
{
    /// <summary>
    /// Central duplicate-prevention rule for job applications. A candidate may
    /// apply to different postings, but only once per posting - matched by
    /// normalized email, normalized phone number, or national ID number.
    /// Shared by the public careers application form and the HR/admin screens
    /// so both workflows enforce exactly the same rule.
    /// </summary>
    public interface IJobApplicationDuplicateService
    {
        /// <summary>
        /// Returns true when an active application for the same posting already
        /// matches the supplied candidate by normalized email, normalized phone
        /// number, or national ID.
        /// </summary>
        /// <param name="excludeApplicationId">
        /// An application that should be ignored (used when editing an existing record).
        /// </param>
        Task<bool> ExistsAsync(
            int jobPostingId,
            string? email,
            string? phone,
            string? nationalIdNumber,
            int? excludeApplicationId = null);
    }
}
