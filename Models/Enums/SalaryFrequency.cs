namespace GameHub.Models.Enums
{
    /// <summary>
    /// How an offered salary is paid. Kept small and explicit so the offer
    /// form can validate the value and the offer letter can render it.
    /// </summary>
    public enum SalaryFrequency
    {
        Monthly,
        Annual,
        Hourly
    }
}
