namespace GameHub.Services.Interfaces
{
    /// <summary>
    /// Renders a simple, professional text document as a PDF byte array.
    /// Used to generate offer letters without an external PDF dependency
    /// (the project ships no PDF library).
    /// </summary>
    public interface ISimplePdfGenerator
    {
        byte[] Generate(string title, IReadOnlyList<string> bodyLines);
    }
}
