namespace GameHub.Services.Interfaces
{
    public interface ICustomerCodeService
    {
        Task<string> GenerateNextCodeAsync();
    }
}
