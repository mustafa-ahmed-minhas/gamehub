namespace GameHub.Services.Interfaces
{
    public interface IMembershipNumberService
    {
        Task<string> GenerateNextNumberAsync();
    }
}
