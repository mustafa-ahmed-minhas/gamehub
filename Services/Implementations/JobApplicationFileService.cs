using GameHub.Services.Interfaces;

namespace GameHub.Services.Implementations
{
    /// <summary>
    /// Stores applicant PDFs under <c>Storage/hr</c> (outside <c>wwwroot</c>) using generated
    /// GUID filenames. Files are only reachable through authorized controller actions, never
    /// through unrestricted public static paths. Executable content and path traversal are
    /// rejected before anything is written to disk.
    /// </summary>
    public class JobApplicationFileService : IJobApplicationFileService
    {
        public const long DefaultMaxPdfBytes = 5 * 1024 * 1024;

        private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46, 0x2D }; // "%PDF-"

        private readonly IWebHostEnvironment _environment;

        public JobApplicationFileService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<PdfSaveResult?> SavePdfAsync(IFormFile? file, string subFolder, long maxBytes = DefaultMaxPdfBytes)
        {
            if (file is null)
            {
                return null;
            }

            if (file.Length <= 0)
            {
                return new PdfSaveResult { Success = false, ErrorMessage = "The selected file is empty. Upload a non-empty PDF." };
            }

            if (file.Length > maxBytes)
            {
                return new PdfSaveResult
                {
                    Success = false,
                    ErrorMessage = $"File size exceeds the {maxBytes / (1024 * 1024)} MB limit."
                };
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new PdfSaveResult { Success = false, ErrorMessage = "Only .pdf files are allowed." };
            }

            var contentType = file.ContentType?.ToLowerInvariant();
            if (contentType is not "application/pdf" and not "application/x-pdf")
            {
                return new PdfSaveResult { Success = false, ErrorMessage = "The selected file is not a valid PDF document." };
            }

            try
            {
                await using var source = file.OpenReadStream();

                // Never trust the extension or client-declared MIME type alone:
                // inspect the actual file content for the PDF header signature.
                var header = new byte[PdfMagic.Length];
                var read = await source.ReadAsync(header.AsMemory(0, header.Length));
                if (read < PdfMagic.Length || !header.AsSpan(0, PdfMagic.Length).SequenceEqual(PdfMagic))
                {
                    return new PdfSaveResult { Success = false, ErrorMessage = "The file content is not a valid PDF. Upload a genuine PDF document." };
                }

                source.Position = 0;

                var storedName = $"{Guid.NewGuid():N}.pdf";
                var destination = Path.Combine(ResolveStorageFolder(subFolder), storedName);

                await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await source.CopyToAsync(target);
                }

                return new PdfSaveResult { Success = true, StoredFileName = storedName };
            }
            catch (IOException)
            {
                return new PdfSaveResult { Success = false, ErrorMessage = "The PDF could not be saved. Please try again." };
            }
            catch (UnauthorizedAccessException)
            {
                return new PdfSaveResult { Success = false, ErrorMessage = "The PDF could not be saved. Please try again." };
            }
        }

        public async Task<string> SaveBytesAsync(byte[] content, string subFolder, string extension = ".pdf")
        {
            if (content is null || content.Length == 0)
            {
                throw new ArgumentException("The document content is empty.", nameof(content));
            }

            var safeExtension = string.IsNullOrWhiteSpace(extension) || extension[0] != '.'
                ? ".pdf"
                : extension;

            var storedName = $"{Guid.NewGuid():N}{safeExtension}";
            var destination = Path.Combine(ResolveStorageFolder(subFolder), storedName);
            await File.WriteAllBytesAsync(destination, content);
            return storedName;
        }

        public Task DeleteAsync(string storedFileName, string subFolder)
        {
            try
            {
                if (TryResolveStoredPath(storedFileName, subFolder, out var path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best effort cleanup; deleting an already missing file is harmless.
            }

            return Task.CompletedTask;
        }

        public async Task<byte[]> ReadAllBytesAsync(string storedFileName, string subFolder)
        {
            if (!TryResolveStoredPath(storedFileName, subFolder, out var path))
            {
                throw new FileNotFoundException("The stored file could not be resolved.");
            }

            return await File.ReadAllBytesAsync(path);
        }

        private string ResolveStorageFolder(string subFolder)
        {
            var safeName = Path.GetFileName(subFolder.Replace('\\', '/').Trim().Trim('/'));
            if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..")
            {
                throw new ArgumentException("Invalid storage sub-folder.", nameof(subFolder));
            }

            var folder = Path.Combine(_environment.ContentRootPath, "Storage", "hr", safeName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Resolves a stored filename to a safe absolute path. The stored name must be a bare
        /// filename (no directory components), preventing path traversal.
        /// </summary>
        private bool TryResolveStoredPath(string storedFileName, string subFolder, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(storedFileName))
            {
                return false;
            }

            var safeName = Path.GetFileName(storedFileName);
            if (safeName != storedFileName || safeName is "." or "..")
            {
                return false;
            }

            if (!string.Equals(Path.GetExtension(safeName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            path = Path.Combine(ResolveStorageFolder(subFolder), safeName);
            return true;
        }
    }
}