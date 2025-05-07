using Microsoft.AspNetCore.Mvc;
using LLM.Services;
using Api.Framework.Result;
using LLM.Models;
using Microsoft.AspNetCore.Authorization;
using NewWords.Api.Repositories; // Added
using SqlSugar; // Added
using NewWords.Api.Entities; // Added
using LLM.Configuration; // Added for LlmConfigurationService
using System.Collections.Generic; // Added for HashSet

namespace NewWords.Api.Controllers;

/// <summary>
/// Controller for testing LLM services including language recognition and word explanations.
/// </summary>
[Authorize]
public class LlmController : BaseController
{
    private readonly LanguageRecognitionService _languageRecognitionService;
    private readonly TranslationAndExplanationService _translationAndExplanationService;
    private readonly LlmConfigurationService _llmConfigService; // Added for accessing agent configurations
    private readonly ISqlSugarClient _dbClient; // Added
    private readonly IWordRepository _wordRepository; // Added
    private readonly IWordCollectionRepository _wordCollectionRepository; // Added
    private readonly ILogger<LlmController> _logger; // Added

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmController"/> class.
    /// </summary>
    /// <param name="languageRecognitionService">The service for language recognition.</param>
    /// <param name="translationAndExplanationService">The service for word explanations and translations.</param>
    /// <param name="llmConfigService">The service for accessing LLM configuration.</param>
    /// <param name="dbClient">The SQLSugar client.</param>
    /// <param name="wordRepository">The repository for Words.</param>
    /// <param name="wordCollectionRepository">The repository for WordCollection.</param>
    /// <param name="logger">The logger instance.</param>
    public LlmController(
        LanguageRecognitionService languageRecognitionService,
        TranslationAndExplanationService translationAndExplanationService,
        LlmConfigurationService llmConfigService, // Added
        ISqlSugarClient dbClient, // Added
        IWordRepository wordRepository, // Added
        IWordCollectionRepository wordCollectionRepository, // Added
        ILogger<LlmController> logger) // Added
    {
        _languageRecognitionService = languageRecognitionService;
        _translationAndExplanationService = translationAndExplanationService;
        _llmConfigService = llmConfigService; // Added
        _dbClient = dbClient; // Added
        _wordRepository = wordRepository; // Added
        _wordCollectionRepository = wordCollectionRepository; // Added
        _logger = logger; // Added
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


                // Get the list of agent configurations
                var agentConfigs = _llmConfigService.GetAgentConfigs();
                if (agentConfigs == null || agentConfigs.Count == 0)
                {
                    return Fail("No LLM providers are configured.");
                }

                // For simplicity, try the first agent only for this endpoint
                // In a full implementation, we could iterate through agents like in FillWordsTable
                var agent = agentConfigs[0];
                _logger.LogInformation("Using agent {AgentProvider} for ExplainWordMarkdown for text '{Text}'", agent.ApiProvider, text);

                // Call the new service method for Markdown with the agent config
                var explanationResult = await _translationAndExplanationService.GetMarkdownExplanationAsync(text, targetLanguage, agent);

                if (explanationResult.IsSuccess && explanationResult.Markdown != null)
                {
                    // Return the Markdown part if successful
                    return new SuccessfulResult<string>(explanationResult.Markdown);
                }
                else
                {
                    // Return failure if the service couldn't get the explanation
                    string errorMsg = explanationResult.ErrorMessage ?? "Unknown error";
                    if (explanationResult.HttpStatusCode.HasValue)
                    {
                        errorMsg += $" (HTTP Status: {explanationResult.HttpStatusCode.Value})";
                    }
                    return Fail($"Could not retrieve explanation for '{text}': {errorMsg}");
                }
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

    /// <summary>
    /// One-off endpoint to populate the Words table with Markdown explanations
    /// for words found in the WordCollection table.
    /// </summary>
    /// <returns>An ApiResult containing the total processed and successfully added counts.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(SuccessfulResult<object>), 200)] // Define response type
    [ProducesResponseType(typeof(FailedResult), 500)] // Define error response type
    public async Task<ApiResult> FillWordsTable()
    {
        const string TARGET_LANGUAGE = "简体中文";
        const string WORD_LANGUAGE = "English";
        const int BATCH_SIZE = 100; // Process 100 words at a time

        long totalProcessed = 0;
        long successfullyAdded = 0;

        try
        {
            // 1. Get Max WordId from Words table
            // Use Queryable<T>().MaxAsync(field) for potentially empty tables
            // If WordId is int, cast might be needed depending on MaxAsync overload resolution
            var maxWordIdObj = await _dbClient.Queryable<Word>().MaxAsync(w => w.WordId);
            long maxWordId = maxWordIdObj == null ? 0 : Convert.ToInt64(maxWordIdObj); // Handle null for empty table

            _logger.LogInformation("Starting FillWordsTable process. Max existing WordId in Words table: {MaxWordId}", maxWordId);

            // 2. Get the list of agent configurations
            var agentConfigs = _llmConfigService.GetAgentConfigs();
            if (agentConfigs == null || agentConfigs.Count == 0)
            {
                _logger.LogError("No LLM providers are configured for FillWordsTable.");
                return Fail("No LLM providers are configured.");
            }
            _logger.LogInformation("Found {AgentCount} LLM providers for FillWordsTable.", agentConfigs.Count);

            // 3. Maintain a set of unavailable providers for this run
            var unavailableProviders = new HashSet<string>();

            // 4. Query WordCollection in batches
            long currentLastId = maxWordId;
            List<WordCollection> wordBatch;

            do
            {
                wordBatch = await _wordCollectionRepository.GetWordsAfterIdAsync(currentLastId, BATCH_SIZE);
                _logger.LogDebug("Fetched {Count} words from WordCollection starting after ID {LastId}", wordBatch.Count, currentLastId);

                if (!wordBatch.Any())
                {
                    _logger.LogInformation("No more words found in WordCollection after ID {LastId}. Ending process.", currentLastId);
                    break; // Exit loop if no more words
                }

                foreach (var wordCollectionRecord in wordBatch)
                {
                    totalProcessed++;
                    currentLastId = wordCollectionRecord.Id; // Update last ID for the next batch query

                    // Skip if WordText is empty or null
                    if (string.IsNullOrWhiteSpace(wordCollectionRecord.WordText))
                    {
                        _logger.LogWarning("Skipping WordCollection record with ID {Id} due to empty WordText.", wordCollectionRecord.Id);
                        continue;
                    }

                    try
                    {
                        // Check if all providers are unavailable before processing this word
                        if (unavailableProviders.Count == agentConfigs.Count)
                        {
                            _logger.LogCritical("All LLM providers are currently unavailable. Aborting FillWordsTable process.");
                            return Fail("All LLM providers are currently unavailable. Please check configurations or try again later.");
                        }

                        // 3a. Iterate through available providers for this word
                        ExplanationResult? successfulResult = null;
                        LlmConfigurationService.AgentConfig? usedAgent = null;

                        foreach (var agent in agentConfigs)
                        {
                            if (unavailableProviders.Contains(agent.ApiProvider))
                            {
                                _logger.LogDebug("Skipping unavailable provider {Provider} for word '{WordText}'", agent.ApiProvider, wordCollectionRecord.WordText);
                                continue;
                            }

                            _logger.LogDebug("Trying provider {Provider} for word '{WordText}'", agent.ApiProvider, wordCollectionRecord.WordText);
                            var explanationResult = await _translationAndExplanationService.GetMarkdownExplanationAsync(wordCollectionRecord.WordText, TARGET_LANGUAGE, agent);

                            if (explanationResult.IsSuccess && explanationResult.Markdown != null)
                            {
                                successfulResult = explanationResult;
                                usedAgent = agent;
                                _logger.LogInformation("Successfully got explanation from provider {Provider} for word '{WordText}' using model {ModelName}", agent.ApiProvider, wordCollectionRecord.WordText, explanationResult.ModelName);
                                break; // Success, no need to try other providers
                            }
                            else
                            {
                                // Log the failure for this provider
                                string errorMsg = explanationResult.ErrorMessage ?? "Unknown error";
                                if (explanationResult.HttpStatusCode.HasValue)
                                {
                                    int statusCode = explanationResult.HttpStatusCode.Value;
                                    errorMsg += $" (HTTP Status: {statusCode})";
                                    // Check if it's a 40x error to mark provider unavailable for this run
                                    if (statusCode >= 400 && statusCode < 500)
                                    {
                                        _logger.LogWarning("Marking provider {Provider} as unavailable for this run due to HTTP {StatusCode} error for word '{WordText}': {ErrorMessage}", agent.ApiProvider, statusCode, wordCollectionRecord.WordText, errorMsg);
                                        unavailableProviders.Add(agent.ApiProvider);
                                    }
                                }
                                _logger.LogWarning("Provider {Provider} failed for word '{WordText}': {ErrorMessage}", agent.ApiProvider, wordCollectionRecord.WordText, errorMsg);
                                // Continue to next provider
                            }
                        }

                        if (successfulResult != null && usedAgent != null)
                        {
                            // 3b. Create Word Entity
                            var newWord = new Word
                            {
                                WordId = (int)wordCollectionRecord.Id, // Cast WordCollection.Id (long) to Word.WordId (int)
                                WordText = wordCollectionRecord.WordText,
                                WordLanguage = WORD_LANGUAGE,
                                ExplanationLanguage = TARGET_LANGUAGE,
                                MarkdownExplanation = successfulResult.Markdown,
                                Pronunciation = null, // Not requested
                                Definitions = null,   // Not requested
                                Examples = null,      // Not requested
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use DateTimeOffset extension
                                ProviderModelName = $"{usedAgent.ApiProvider}:{successfulResult.ModelName}"
                            };

                            // 3c. Insert into Words Table
                            try
                            {
                                // Use InsertAsync from RepositoryBase
                                var insertResult = await _wordRepository.InsertAsync(newWord);
                                if (insertResult) // InsertAsync returns bool indicating success
                                {
                                    successfullyAdded++;
                                    _logger.LogDebug("Successfully added WordId: {WordId}, Text: {WordText}", newWord.WordId, newWord.WordText);
                                }
                                else
                                {
                                    // This case might indicate an issue if InsertAsync is expected to throw on failure
                                    _logger.LogWarning("Failed to insert WordId: {WordId}, Text: {WordText} (InsertAsync returned false)", newWord.WordId, newWord.WordText);
                                }
                            }
                            catch (Exception dbEx)
                            {
                                // Catch potential exceptions like unique key violations if the word already exists
                                _logger.LogError(dbEx, "Database error inserting WordId: {WordId}, Text: {WordText}. Possible duplicate?", wordCollectionRecord.Id, wordCollectionRecord.WordText);
                                // Continue to next word
                            }
                        }
                        else
                        {
                            // No provider succeeded for this word
                            _logger.LogWarning("All available providers failed to provide explanation for WordCollection ID: {WordCollectionId}, Text: {WordText}.", wordCollectionRecord.Id, wordCollectionRecord.WordText);
                            // Continue to next word
                        }
                    }
                    catch (Exception serviceEx) // Catch unexpected errors from the loop/service call itself
                    {
                        _logger.LogError(serviceEx, "Error processing WordCollection ID: {WordCollectionId}, Text: {WordText}", wordCollectionRecord.Id, wordCollectionRecord.WordText);
                        // Continue to next word
                    }

                    // Optional: Add a small delay to avoid overwhelming the LLM API
                    // await Task.Delay(100);
                }

            } while (wordBatch.Count == BATCH_SIZE); // Continue if we likely have more batches

            _logger.LogInformation("FillWordsTable process finished. Total Processed: {TotalProcessed}, Successfully Added: {SuccessfullyAdded}", totalProcessed, successfullyAdded);
            // 4. Return Result using ApiResult structure
            return new SuccessfulResult<object>(new { TotalProcessed = totalProcessed, SuccessfullyAdded = successfullyAdded });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during FillWordsTable process.");
            return Fail($"An error occurred during the FillWordsTable process: {ex.Message}");
        }
    }
}
