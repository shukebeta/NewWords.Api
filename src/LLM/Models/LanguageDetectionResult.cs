namespace LLM.Models;

/// <summary>
/// Represents the result of language recognition for a given word or phrase.
/// </summary>
public class LanguageDetectionResult
{
    public string LanguageCode { get; set; } = string.Empty;
    public decimal ConfidenceLevel { get; set; }
}
