using GameHub.Models.Entities;

namespace GameHub.Services.Interfaces
{
    /// <summary>
    /// Publishes the real-time website-inquiry signal used by the ERP:
    /// the "New Website Inquiries" counter and the new-inquiry toast.
    /// <para>
    /// This is the single source of truth for the counter definition and the
    /// SignalR event contract, so the server-rendered counter, the JSON fallback
    /// endpoint and the real-time channel can never disagree.
    /// </para>
    /// <para>
    /// Broadcasts are best-effort: implementations must never throw because the
    /// inquiry is already persisted before this service is invoked.
    /// </para>
    /// </summary>
    public interface IWebsiteInquiryNotifier
    {
        /// <summary>
        /// Number of active website inquiries still awaiting review.
        /// A website inquiry is "awaiting review" until its workflow status
        /// moves off <c>New</c> (the existing Lead Management progressions:
        /// contact, follow up, qualify, disqualify or convert).
        /// </summary>
        Task<int> GetUnreviewedCountAsync();

        /// <summary>
        /// Broadcasts a new-inquiry notification (toast payload + fresh counter)
        /// to every connected ERP tab.
        /// </summary>
        Task BroadcastNewInquiryAsync(Lead lead);

        /// <summary>
        /// Broadcasts the latest counter value to every connected ERP tab.
        /// Used after a website inquiry is reviewed (its status changes).
        /// </summary>
        Task BroadcastCountAsync();

        /// <summary>
        /// Sends the latest counter value to a single freshly connected client,
        /// so a new or reconnected tab cannot drift from the database.
        /// </summary>
        Task SendCountToConnectionAsync(string connectionId);
    }
}
