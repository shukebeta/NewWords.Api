namespace LLM.Models;

/// <summary>
/// Represents the result of a story generation attempt.
/// </summary>
public class StoryResult
{
    public bool IsSuccess { get; set; }
    public string? Content { get; set; }
    public string? ModelName { get; set; }
    public int? HttpStatusCode { get; set; } // To store the status code on failure
    public string? ErrorMessage { get; set; }
}