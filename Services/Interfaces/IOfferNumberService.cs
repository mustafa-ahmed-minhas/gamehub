namespace GameHub.Services.Interfaces
{
    public interface IOfferNumberService
    {
        Task<string> GenerateNextReferenceAsync();
    }
}
