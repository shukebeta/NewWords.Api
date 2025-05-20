using Flurl.Http;
using LLM.Models;

namespace LLM.Services;

/// <summary>
/// Language Service, fully utilizing Flurl features
/// </summary>
public class LanguageService()
{
    /// <summary>
    /// Get Markdown formatted explanation using Flurl features
    /// </summary>
    /// <param name="inputText">Input text</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="apiBaseUrl">API base URL</param>
    /// <param name="apiKey">API key</param>
    /// <param name="model">Model name</param>
    /// <returns>Explanation result</returns>
    public async Task<ExplanationResult> GetMarkdownExplanationAsync(
        string inputText,
        string targetLanguage,
        string apiBaseUrl,
        string apiKey,
        string model)
    {
        // Validate input
        if (string.IsNullOrEmpty(inputText)) throw new ArgumentException("Input text cannot be empty", nameof(inputText));
        if (string.IsNullOrEmpty(targetLanguage)) throw new ArgumentException("Target language cannot be empty", nameof(targetLanguage));
        if (string.IsNullOrEmpty(apiBaseUrl)) throw new ArgumentException("API base URL cannot be empty", nameof(apiBaseUrl));
        if (string.IsNullOrEmpty(apiKey)) throw new ArgumentException("API key cannot be empty", nameof(apiKey));
        if (string.IsNullOrEmpty(model)) throw new ArgumentException("Model name cannot be empty", nameof(model));

        try
        {
            // Build the complete API endpoint
            string apiUrl = apiBaseUrl.TrimEnd('/') + "/chat/completions";

            // Build the prompt
            var userPrompt = $@"You are a language learning assistant that provides detailed explanations when given a word or phrase. When I provide a word or phrase in any language and specify my target Language ""{targetLanguage}"", please respond with:

1. The word/phrase with its IPA Transcription
2. Any relevant grammatical information (tense, part of speech, etc.) in ""{targetLanguage}""
3. A clear definition
4. 2-3 example sentences using the word/phrase in original language with translations
5. 3-4 related words/synonyms/antonyms with pronunciations and translations and one example sentence

If I provide a complete sentence instead of a single word or phrase, please:
1. Provide a clear translation of the full sentence in the target language
2. Break down the sentence structure and explain its grammatical components
3. Identify and explain any idioms, colloquialisms, or potentially difficult parts of the sentence
4. Provide alternative ways to express the same meaning

Important: Do not label translations with ""{targetLanguage}:"" before each translated sentence. Simply provide the translation directly after the original sentence.

Format your response in a clear, structured way with bold headings and good spacing to make it easy to read. 
**IMPORTANT:**
RESPOND WITH THE MARKDOWN CONTENT ONLY.
DO NOT INCLUDE ANY INTRODUCTORY TEXT, CONCLUDING REMARKS, OR CODE FENCES (like ```markdown)'

My request is: {inputText}";

            var systemPrompt = "You are a linguistic expert generating helpful, concise Markdown explanations for language learners. Respond ONLY with the requested Markdown text, nothing else.";

            // Build the request body
            var requestData = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3f
            };

            // Use Flurl's fluent API to send request and directly parse JSON response
            var response = await apiUrl
                .WithHeader("Authorization", $"Bearer {apiKey}")
                .PostJsonAsync(requestData)
                .ReceiveJson<ApiCompletionResponse>();

            // Parse the response
            if (response?.Choices != null && response.Choices.Length > 0 &&
                !string.IsNullOrWhiteSpace(response.Choices[0].Message.Content))
            {
                return new ExplanationResult
                {
                    IsSuccess = true,
                    Markdown = response.Choices[0].Message.Content.Trim(),
                    ModelName = model
                };
            }

            return new ExplanationResult
            {
                IsSuccess = false,
                ModelName = model,
                ErrorMessage = "No valid content in the API response"
            };
        }
        catch (FlurlHttpException ex)
        {
            // Use Flurl's exception handling to get detailed information
            string errorDetail = await ex.GetResponseStringAsync().ConfigureAwait(false);

            return new ExplanationResult
            {
                IsSuccess = false,
                HttpStatusCode = ex.StatusCode,
                ModelName = model,
                ErrorMessage = $"HTTP error {ex.StatusCode}: {ex.Message}. Details: {errorDetail}"
            };
        }
        catch (Exception ex)
        {
            return new ExplanationResult
            {
                IsSuccess = false,
                ModelName = model,
                ErrorMessage = $"An exception occurred: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Attempt to get explanation using multiple models, trying in sequence until successful
    /// </summary>
    public async Task<ExplanationResult> GetMarkdownExplanationWithFallbackAsync(
        string inputText,
        string targetLanguage,
        string apiBaseUrl,
        string apiKey,
        string[] models)
    {
        if (models == null || models.Length == 0)
            throw new ArgumentException("At least one model must be provided", nameof(models));

        foreach (var model in models)
        {
            var result = await GetMarkdownExplanationAsync(inputText, targetLanguage, apiBaseUrl, apiKey, model);
            if (result.IsSuccess)
                return result;
        }

        // All models failed, return the last error
        return new ExplanationResult
        {
            IsSuccess = false,
            ModelName = models.LastOrDefault(),
            ErrorMessage = "No valid model provided"
        };
    }
}
