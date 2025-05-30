using LLM.Models;

namespace LLM;

public interface ILanguageService
{
    Task<ExplanationResult> GetMarkdownExplanationAsync(
        string inputText,
        string nativeLanguage,
        string targetLanguage,
        string apiBaseUrl,
        string apiKey,
        string model);
}
