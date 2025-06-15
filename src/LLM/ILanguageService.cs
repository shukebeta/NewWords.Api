using LLM.Models;
namespace LLM;

public interface ILanguageService
{
    Task<ExplanationResult> GetMarkdownExplanationWithFallbackAsync(
        string inputText,
        string nativeLanguageName,
        string targetLanguageName);

    /// <summary>
    /// Detects the language of the given text and returns LanguageDetectionResult that contains the language code along with the confidence level.
    /// This method is asynchronous and can be used to identify the language of a string input.
    /// The language code is typically a two-letter ISO 639-1 code (e.g., "en" for English, "fr" for French).
    /// However, for simplified-chinese, it returns zh-CN, and for traditional-chinese, it returns zh-TW.
    /// The confidence level is a decimal value between 0 and 1, indicating how confident the service is about the detected language.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    Task<LanguageDetectionResult> GetDetectedLanguageWithFallbackAsync(string text);
}
