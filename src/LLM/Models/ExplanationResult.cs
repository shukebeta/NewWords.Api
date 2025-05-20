namespace LLM.Models;

/// <summary>
/// Represents the result of an explanation attempt.
/// </summary>
public class ExplanationResult
{
    public bool IsSuccess { get; set; }
    public string? Markdown { get; set; }
    public string? ModelName { get; set; }
    public int? HttpStatusCode { get; set; } // To store the status code on failure
    public string? ErrorMessage { get; set; }
}
