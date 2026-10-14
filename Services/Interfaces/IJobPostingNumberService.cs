namespace GameHub.Services.Interfaces
{
    public interface IJobPostingNumberService
    {
        Task<string> GenerateNextReferenceAsync();
    }
}