namespace GameHub.Services
{
    /// <summary>
    /// Result of securely persisting a PDF upload.
    /// <see cref="StoredFileName"/> is the generated filename on disk (never the client-supplied name).
    /// </summary>
    public class PdfSaveResult
    {
        public bool Success { get; set; }

        public string? StoredFileName { get; set; }

        public string? ErrorMessage { get; set; }
    }
}