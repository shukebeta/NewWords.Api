using Microsoft.AspNetCore.Mvc;
using LLM.Services;
using Api.Framework.Result;
using System.Threading.Tasks;
using LLM.Models;
using Microsoft.AspNetCore.Authorization;

namespace NewWords.Api.Controllers;

/// <summary>
/// Controller for testing LLM services including language recognition and word explanations.
/// </summary>
[Authorize]
public class LlmController : BaseController
{
    private readonly LanguageRecognitionService _languageRecognitionService;
    private readonly TranslationAndExplanationService _translationAndExplanationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmController"/> class.
    /// </summary>
    /// <param name="languageRecognitionService">The service for language recognition.</param>
    /// <param name="translationAndExplanationService">The service for word explanations and translations.</param>
    public LlmController(
        LanguageRecognitionService languageRecognitionService,
        TranslationAndExplanationService translationAndExplanationService)
    {
        _languageRecognitionService = languageRecognitionService;
        _translationAndExplanationService = translationAndExplanationService;
    }

    /// <summary>
    /// Recognizes the language of the provided text.
    /// </summary>
    /// <param name="text">The text to analyze for language recognition.</param>
    /// <returns>A result containing the recognized languages with confidence scores.</returns>
    [HttpGet("RecognizeLanguage")] // Added route template for clarity
    public async Task<ApiResult> RecognizeLanguage([FromQuery] string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Fail("Text parameter is required.");
        }

        var result = await _languageRecognitionService.RecognizeLanguageAsync(text);
        return new SuccessfulResult<LanguageRecognitionResult>(result);
    }

    // /// <summary>
    // /// Provides a detailed explanation of the provided word or phrase in the target language. (OLD METHOD - COMMENTED OUT)
    // /// </summary>
    // /// <param name="text">The word or phrase to explain.</param>
    // /// <param name="targetLanguage">The language in which to provide the explanation.</param>
    // /// <returns>A result containing the detailed explanation including phonetic transcription.</returns>
    // [HttpPost]
    // public async Task<ApiResult> ExplainWord([FromQuery] string text, [FromQuery] string targetLanguage)
    // {
    //     if (string.IsNullOrEmpty(text))
    //     {
    //         return Fail("Text parameter is required.");
    //     }
    //     if (string.IsNullOrEmpty(targetLanguage))
    //     {
    //         return Fail("Target language parameter is required.");
    //     }
    //
    //     // This method no longer exists in the service
    //     // var result = await _translationAndExplanationService.ExplainWordAsync(text, targetLanguage);
    //     // return new SuccessfulResult<WordExplanationResult>(result);
    //     return Fail("Endpoint deprecated. Use ExplainWordMarkdown or ExplainWordStructured.");
    // }

    /// <summary>
    /// Provides a Markdown-formatted explanation of the provided word or phrase.
    /// </summary>
    /// <param name="text">The word or phrase to explain.</param>
    /// <param name="targetLanguage">The language for the explanation.</param>
    /// <returns>A result containing the Markdown explanation string.</returns>
    [HttpGet] // New endpoint
    public async Task<ApiResult> ExplainWordMarkdown([FromQuery] string text, [FromQuery] string targetLanguage)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Fail("Text parameter is required.");
        }
        if (string.IsNullOrEmpty(targetLanguage))
        {
            return Fail("Target language parameter is required.");
        }

        // Call the new service method for Markdown
        var markdownResult = await _translationAndExplanationService.GetMarkdownExplanationAsync(text, targetLanguage);
        // Return as plain text for easy viewing/testing, or wrap in ApiResult if preferred
        // return Content(markdownResult, "text/markdown"); // Option 1: Return raw markdown
        return new SuccessfulResult<string>(markdownResult); // Option 2: Wrap in standard ApiResult
    }

    /// <summary>
    /// Provides structured linguistic details (IPA, translations, etc.) for the word or phrase.
    /// </summary>
    /// <param name="text">The word or phrase to analyze.</param>
    /// <param name="targetLanguage">The language for the analysis.</param>
    /// <returns>A result containing the structured WordExplanationResult object.</returns>
    [HttpGet] // New endpoint
    public async Task<ApiResult> ExplainWordStructured([FromQuery] string text, [FromQuery] string targetLanguage)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Fail("Text parameter is required.");
        }
        if (string.IsNullOrEmpty(targetLanguage))
        {
            return Fail("Target language parameter is required.");
        }

        // Call the new service method for structured data
        var structuredResult = await _translationAndExplanationService.GetStructuredExplanationAsync(text, targetLanguage);
        return new SuccessfulResult<WordExplanationResult>(structuredResult);
    }
}
