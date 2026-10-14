namespace GameHub.Services.Interfaces
{
    public interface IPerformanceNumberService
    {
        Task<string> GenerateNextCaseNumberAsync();
    }
}
