namespace GameHub.Services.Interfaces
{
    /// <summary>
    /// Generates the sequential, human-readable reference used by termination
    /// requests (e.g. TRM-1, TRM-2), mirroring the other HR numbering services.
    /// </summary>
    public interface ITerminationNumberService
    {
        Task<string> GenerateNextReferenceAsync();
    }
}
