namespace GameHub.Services.Interfaces
{
    public interface IJobApplicationNumberService
    {
        Task<string> GenerateNextReferenceAsync();
    }
}