namespace GameHub.Services.Interfaces
{
    public interface IBookingNumberService
    {
        Task<string> GenerateNextNumberAsync();
    }
}
