namespace GameHub.Services.Interfaces
{
    public interface IEmployeeNumberService
    {
        Task<string> GenerateNextEmployeeNumberAsync();
    }
}