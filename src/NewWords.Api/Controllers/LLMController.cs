using Microsoft.AspNetCore.Mvc;
using LLM.Services;
using Api.Framework.Result;
using LLM.Models;
using Microsoft.AspNetCore.Authorization;
using SqlSugar;
using NewWords.Api.Entities;
using Api.Framework.Extensions;
using LLM;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Controllers;

/// <summary>
/// Controller for testing LLM services including language recognition and word explanations.
/// </summary>
[Authorize]
public class LlmController(
    ILanguageService languageService,
    ISqlSugarClient dbClient,
    IConfigurationService configurationService,
    ILogger<LlmController> logger)
    : BaseController
{
    /// <summary>
    /// Endpoint to recognize the language of a given text. We should use a speedy language recognition model for it
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<ApiResult> RecognizeLanguage([FromQuery] string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Fail("Text parameter is required.");
        }
        var result = await languageService.GetDetectedLanguageWithFallbackAsync(text);
        return new SuccessfulResult<LanguageDetectionResult>(result);
    }

    [HttpGet]
    public async Task<ApiResult> ExplainWordMarkdown([FromQuery] string text, [FromQuery] string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return Fail("Text parameter is required.");
        if (string.IsNullOrEmpty(targetLanguage)) return Fail("Target language parameter is required.");

        var nativeLanguageName = configurationService.GetLanguageName("zh-CN") ?? "Chinese (Simplified)";
        var targetLanguageName = configurationService.GetLanguageName(targetLanguage) ?? targetLanguage;
        var explanationResult = await languageService.GetMarkdownExplanationWithFallbackAsync(text, nativeLanguageName, targetLanguageName);
        if (explanationResult.IsSuccess && explanationResult.Markdown != null)
        {
            return new SuccessfulResult<string>(explanationResult.Markdown);
        }

        string errorMsg = explanationResult.ErrorMessage ?? "Unknown error";
        if (explanationResult.HttpStatusCode.HasValue)
        {
            errorMsg += $" (HTTP Status: {explanationResult.HttpStatusCode.Value})";
        }
        return Fail($"Could not retrieve explanation for '{text}': {errorMsg}");
    }

    /// <summary>
    /// One-off endpoint to populate the WordExplanations table with Markdown explanations
    /// for words found in the WordCollection table.
    /// </summary>
    /// <returns>An ApiResult containing the total processed, skipped, and successfully added counts.</returns>
    [HttpPost] // Renamed endpoint for clarity
    [ProducesResponseType(typeof(SuccessfulResult<object>), 200)]
    [ProducesResponseType(typeof(FailedResult), 500)]
    public async Task<ApiResult> FillWordExplanationsTable()
    {
        const string NativeLanguageCode = "zh-CN";
        const string LearnLanguageCode = "en";
        const int BATCH_SIZE = 50;

        long totalProcessed = 0;
        long successfullyAdded = 0;
        long skippedExisting = 0;
        try
        {
            logger.LogInformation("Starting FillWordExplanationsTable process for TargetExplanationLanguage: {TargetLang}, LearningLanguage: {LearnLang}",
                NativeLanguageCode, LearnLanguageCode);

            var nativeLanguageName = configurationService.GetLanguageName(NativeLanguageCode) ?? "Chinese (Simplified)";
            var learningLanguageName = configurationService.GetLanguageName(LearnLanguageCode) ?? "English";

            long currentLastId = 0; // Start from the beginning of WordCollection
            List<WordCollection> wordCollectionBatch;

            do
            {
                // Fetch words from WordCollection (language-agnostic)
                wordCollectionBatch = await dbClient.Queryable<WordCollection>()
                                           .Where(wc => wc.Id > currentLastId && wc.DeletedAt == null)
                                           .OrderBy(wc => wc.Id)
                                           .Take(BATCH_SIZE)
                                           .ToListAsync();

                logger.LogDebug("Fetched {Count} words from WordCollection starting after ID {LastId}",
                                 wordCollectionBatch.Count, currentLastId);

                if (!wordCollectionBatch.Any())
                {
                    logger.LogInformation("No more words found in WordCollection after ID {LastId}. Ending process.",
                                           currentLastId);
                    break;
                }

                foreach (var wcRecord in wordCollectionBatch)
                {
                    totalProcessed++;
                    currentLastId = wcRecord.Id;

                    if (string.IsNullOrWhiteSpace(wcRecord.WordText))
                    {
                        logger.LogWarning("Skipping WordCollection record with ID {Id} due to empty WordText.", wcRecord.Id);
                        continue;
                    }

                    // Check if an explanation already exists for this WordCollectionId and TargetExplanationLanguage
                    bool explanationExists = await dbClient.Queryable<WordExplanation>()
                        .AnyAsync(we => we.WordCollectionId == wcRecord.Id && we.ExplanationLanguage == NativeLanguageCode);

                    if (explanationExists)
                    {
                        skippedExisting++;
                        logger.LogDebug("Skipping WordCollection ID: {Id}, Text: {Text}. Explanation already exists for language {TargetLang}.",
                                         wcRecord.Id, wcRecord.WordText, NativeLanguageCode);
                        continue;
                    }


                    try
                    {

                        var explanationResult = await languageService.GetMarkdownExplanationWithFallbackAsync(
                            wcRecord.WordText,
                            nativeLanguageName,
                            learningLanguageName);

                        if (explanationResult.IsSuccess && !string.IsNullOrWhiteSpace(explanationResult.Markdown))
                        {
                            var newExplanation = new WordExplanation
                            {
                                WordCollectionId = wcRecord.Id,
                                WordText = wcRecord.WordText, // Denormalized
                                LearningLanguage = LearnLanguageCode, // User's learning language
                                ExplanationLanguage = NativeLanguageCode,
                                MarkdownExplanation = explanationResult.Markdown,
                                CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds(),
                                ProviderModelName = $"{explanationResult.ModelName}"
                            };

                            try
                            {
                                // Using direct DB client insert for WordExplanation
                                var insertResult = await dbClient.Insertable(newExplanation).ExecuteCommandAsync();
                                if (insertResult > 0)
                                {
                                    successfullyAdded++;
                                    logger.LogDebug("Successfully added WordExplanation for WC ID: {WCId}, Text: {WordText}",
                                                     wcRecord.Id, wcRecord.WordText);
                                }
                                else
                                {
                                    logger.LogWarning("Failed to insert WordExplanation for WC ID: {WCId}, Text: {WordText} (ExecuteCommandAsync returned 0)",
                                                     wcRecord.Id, wcRecord.WordText);
                                }
                            }
                            catch (Exception dbEx)
                            {
                                logger.LogError(dbEx, "Database error inserting WordExplanation for WC ID: {WCId}, Text: {WordText}. Possible duplicate or other constraint violation?",
                                                 wcRecord.Id, wcRecord.WordText);
                            }
                        }
                        else
                        {
                            logger.LogWarning("All available providers failed for WordCollection ID: {WCId}, Text: {WordText}.",
                                             wcRecord.Id, wcRecord.WordText);
                        }
                    }
                    catch (Exception serviceEx)
                    {
                        logger.LogError(serviceEx, "Error processing WordCollection ID: {WCId}, Text: {WordText}",
                                         wcRecord.Id, wcRecord.WordText);
                    }
                    // await Task.Delay(100); // Optional delay
                }
            } while (wordCollectionBatch.Any());

            logger.LogInformation("FillWordExplanationsTable process finished. Total Processed: {TotalProcessed}, Successfully Added: {SuccessfullyAdded}, Skipped (Existing): {SkippedExisting}",
                                 totalProcessed, successfullyAdded, skippedExisting);
            return new SuccessfulResult<object>(new
            {
                TotalProcessed = totalProcessed,
                SuccessfullyAdded = successfullyAdded,
                SkippedExistingExplanation = skippedExisting
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception during FillWordExplanationsTable process.");
            return Fail($"An error occurred: {ex.Message}");
        }
    }
}
