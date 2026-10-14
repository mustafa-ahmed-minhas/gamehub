namespace GameHub.Services.Interfaces
{
    public interface IJobApplicationFileService
    {
        /// <summary>
        /// Validates an uploaded PDF (extension, content type, magic bytes, size) and stores it
        /// outside <c>wwwroot</c> under a generated filename. Returns null when no file is supplied.
        /// </summary>
        Task<PdfSaveResult?> SavePdfAsync(IFormFile? file, string subFolder, long maxBytes = 5 * 1024 * 1024);

        /// <summary>
        /// Persists already-generated document bytes (e.g. a system-generated offer letter)
        /// under the same secure storage root as uploads. Returns the generated filename.
        /// </summary>
        Task<string> SaveBytesAsync(byte[] content, string subFolder, string extension = ".pdf");

        Task DeleteAsync(string storedFileName, string subFolder);

        Task<byte[]> ReadAllBytesAsync(string storedFileName, string subFolder);
    }
}