using Microsoft.AspNetCore.Mvc;
using LLM.Services;
using Api.Framework.Result;
using LLM.Models;
using Microsoft.AspNetCore.Authorization;
using NewWords.Api.Repositories;
using SqlSugar;
using NewWords.Api.Entities;
using LLM.Configuration;
using System.Collections.Generic; // Added for HashSet
using System.Linq; // Added for Linq
using System.Threading.Tasks; // Added for Task
using Api.Framework.Extensions; // Added for ToUnixTimeSeconds
using System; // Added for Exception

namespace NewWords.Api.Controllers;

/// <summary>
/// Controller for testing LLM services including language recognition and word explanations.
/// </summary>
[Authorize]
public class LlmController : BaseController
{
    private readonly LanguageRecognitionService _languageRecognitionService;
    private readonly TranslationAndExplanationService _translationAndExplanationService;
    private readonly LlmConfigurationService _llmConfigService;
    private readonly ISqlSugarClient _dbClient;
    private readonly IWordRepository _wordExplanationRepository; // Renamed to reflect it handles WordExplanation
    private readonly IWordCollectionRepository _wordCollectionRepository;
    private readonly ILogger<LlmController> _logger;

    public LlmController(
        LanguageRecognitionService languageRecognitionService,
        TranslationAndExplanationService translationAndExplanationService,
        LlmConfigurationService llmConfigService,
        ISqlSugarClient dbClient,
        IWordRepository wordExplanationRepository, // Renamed
        IWordCollectionRepository wordCollectionRepository,
        ILogger<LlmController> logger)
    {
        _languageRecognitionService = languageRecognitionService;
        _translationAndExplanationService = translationAndExplanationService;
        _llmConfigService = llmConfigService;
        _dbClient = dbClient;
        _wordExplanationRepository = wordExplanationRepository; // Renamed
        _wordCollectionRepository = wordCollectionRepository;
        _logger = logger;
    }

    [HttpGet("RecognizeLanguage")]
    public async Task<ApiResult> RecognizeLanguage([FromQuery] string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Fail("Text parameter is required.");
        }
        var result = await _languageRecognitionService.RecognizeLanguageAsync(text);
        return new SuccessfulResult<LanguageRecognitionResult>(result);
    }

    [HttpGet("ExplainWordMarkdown")]
    public async Task<ApiResult> ExplainWordMarkdown([FromQuery] string text, [FromQuery] string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return Fail("Text parameter is required.");
        if (string.IsNullOrEmpty(targetLanguage)) return Fail("Target language parameter is required.");

        var agentConfigs = _llmConfigService.GetAgentConfigs();
        if (agentConfigs == null || !agentConfigs.Any())
        {
            return Fail("No LLM providers are configured.");
        }
        var agent = agentConfigs.First();
        _logger.LogInformation("Using agent {AgentProvider} for ExplainWordMarkdown for text '{Text}'", agent.ApiProvider, text);

        var explanationResult = await _translationAndExplanationService.GetMarkdownExplanationAsync(text, targetLanguage, agent);

        if (explanationResult.IsSuccess && explanationResult.Markdown != null)
        {
            return new SuccessfulResult<string>(explanationResult.Markdown);
        }
        else
        {
            string errorMsg = explanationResult.ErrorMessage ?? "Unknown error";
            if (explanationResult.HttpStatusCode.HasValue)
            {
                errorMsg += $" (HTTP Status: {explanationResult.HttpStatusCode.Value})";
            }
            return Fail($"Could not retrieve explanation for '{text}': {errorMsg}");
        }
    }

    [HttpGet("ExplainWordStructured")]
    public async Task<ApiResult> ExplainWordStructured([FromQuery] string text, [FromQuery] string targetLanguage)
    {
        if (string.IsNullOrEmpty(text)) return Fail("Text parameter is required.");
        if (string.IsNullOrEmpty(targetLanguage)) return Fail("Target language parameter is required.");
        
        // Note: GetStructuredExplanationAsync was marked as NotImplementedException in TranslationAndExplanationService
        // This will likely throw an exception until that service method is fully implemented.
        try
        {
            var structuredResult = await _translationAndExplanationService.GetStructuredExplanationAsync(text, targetLanguage);
            return new SuccessfulResult<WordExplanationResult>(structuredResult);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogError(ex, "ExplainWordStructured endpoint hit a NotImplementedException from the service.");
            return Fail("This feature (structured explanation) is not fully implemented yet.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExplainWordStructured for text '{Text}' and target language '{TargetLanguage}'", text, targetLanguage);
            return Fail($"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// One-off endpoint to populate the WordExplanations table with Markdown explanations
    /// for words found in the WordCollection table.
    /// </summary>
    /// <returns>An ApiResult containing the total processed, skipped, and successfully added counts.</returns>
    [HttpPost("FillWordExplanationsTable")] // Renamed endpoint for clarity
    [ProducesResponseType(typeof(SuccessfulResult<object>), 200)]
    [ProducesResponseType(typeof(FailedResult), 500)]
    public async Task<ApiResult> FillWordExplanationsTable()
    {
        const string TARGET_EXPLANATION_LANGUAGE = "zh-CN"; // Example: Target language for explanations
        const string SOURCE_WORD_LANGUAGE = "en";      // Example: Process only English words from WordCollection
        const int BATCH_SIZE = 50;

        long totalProcessed = 0;
        long successfullyAdded = 0;
        long skippedExisting = 0;
        long skippedWrongLanguage = 0;

        try
        {
            _logger.LogInformation("Starting FillWordExplanationsTable process for TargetExplanationLanguage: {TargetLang}, SourceWordLanguage: {SourceLang}",
                TARGET_EXPLANATION_LANGUAGE, SOURCE_WORD_LANGUAGE);

            var agentConfigs = _llmConfigService.GetAgentConfigs();
            if (agentConfigs == null || !agentConfigs.Any())
            {
                _logger.LogError("No LLM providers are configured for FillWordExplanationsTable.");
                return Fail("No LLM providers are configured.");
            }
            _logger.LogInformation("Found {AgentCount} LLM providers.", agentConfigs.Count);

            var unavailableProviders = new HashSet<string>();
            long currentLastId = 0; // Start from the beginning of WordCollection
            List<WordCollection> wordCollectionBatch;

            do
            {
                // Fetch words from WordCollection where Language matches SOURCE_WORD_LANGUAGE
                wordCollectionBatch = await _dbClient.Queryable<WordCollection>()
                                           .Where(wc => wc.Id > currentLastId && wc.Language == SOURCE_WORD_LANGUAGE && wc.DeletedAt == null)
                                           .OrderBy(wc => wc.Id)
                                           .Take(BATCH_SIZE)
                                           .ToListAsync();

                _logger.LogDebug("Fetched {Count} words from WordCollection (Language: {SourceLang}) starting after ID {LastId}",
                                 wordCollectionBatch.Count, SOURCE_WORD_LANGUAGE, currentLastId);

                if (!wordCollectionBatch.Any())
                {
                    _logger.LogInformation("No more words found in WordCollection for language {SourceLang} after ID {LastId}. Ending process.",
                                           SOURCE_WORD_LANGUAGE, currentLastId);
                    break;
                }

                foreach (var wcRecord in wordCollectionBatch)
                {
                    totalProcessed++;
                    currentLastId = wcRecord.Id;

                    if (string.IsNullOrWhiteSpace(wcRecord.WordText))
                    {
                        _logger.LogWarning("Skipping WordCollection record with ID {Id} due to empty WordText.", wcRecord.Id);
                        continue;
                    }

                    // Check if an explanation already exists for this WordCollectionId and TargetExplanationLanguage
                    bool explanationExists = await _dbClient.Queryable<WordExplanation>()
                        .AnyAsync(we => we.WordCollectionId == wcRecord.Id && we.ExplanationLanguage == TARGET_EXPLANATION_LANGUAGE);

                    if (explanationExists)
                    {
                        skippedExisting++;
                        _logger.LogDebug("Skipping WordCollection ID: {Id}, Text: {Text}. Explanation already exists for language {TargetLang}.",
                                         wcRecord.Id, wcRecord.WordText, TARGET_EXPLANATION_LANGUAGE);
                        continue;
                    }

                    // This check is now part of the initial query, but kept for safety / clarity if query changes
                    if (wcRecord.Language != SOURCE_WORD_LANGUAGE)
                    {
                        skippedWrongLanguage++;
                         _logger.LogDebug("Skipping WordCollection ID: {Id}, Text: {Text}. Language '{ActualLang}' does not match target '{TargetSourceLang}'.",
                                         wcRecord.Id, wcRecord.WordText, wcRecord.Language, SOURCE_WORD_LANGUAGE);
                        continue;
                    }

                    try
                    {
                        if (unavailableProviders.Count == agentConfigs.Count)
                        {
                            _logger.LogCritical("All LLM providers are currently unavailable. Aborting FillWordExplanationsTable process.");
                            return Fail("All LLM providers are currently unavailable.");
                        }

                        ExplanationResult? successfulResult = null;
                        LlmConfigurationService.AgentConfig? usedAgent = null;

                        foreach (var agent in agentConfigs)
                        {
                            if (unavailableProviders.Contains(agent.ApiProvider)) continue;

                            _logger.LogDebug("Trying provider {Provider} for word '{WordText}' (WC ID: {WCId})",
                                             agent.ApiProvider, wcRecord.WordText, wcRecord.Id);
                            var explanationResult = await _translationAndExplanationService.GetMarkdownExplanationAsync(wcRecord.WordText, TARGET_EXPLANATION_LANGUAGE, agent);

                            if (explanationResult.IsSuccess && !string.IsNullOrWhiteSpace(explanationResult.Markdown))
                            {
                                successfulResult = explanationResult;
                                usedAgent = agent;
                                _logger.LogInformation("Successfully got explanation from provider {Provider} for word '{WordText}' (WC ID: {WCId}) using model {ModelName}",
                                                       agent.ApiProvider, wcRecord.WordText, wcRecord.Id, explanationResult.ModelName);
                                break;
                            }
                            
                            string errorMsg = explanationResult.ErrorMessage ?? "Unknown error";
                            if (explanationResult.HttpStatusCode.HasValue)
                            {
                                int statusCode = explanationResult.HttpStatusCode.Value;
                                errorMsg += $" (HTTP Status: {statusCode})";
                                if (statusCode >= 400 && statusCode < 500)
                                {
                                    _logger.LogWarning("Marking provider {Provider} as unavailable due to HTTP {StatusCode} for word '{WordText}' (WC ID: {WCId}): {Error}",
                                                       agent.ApiProvider, statusCode, wcRecord.WordText, wcRecord.Id, errorMsg);
                                    unavailableProviders.Add(agent.ApiProvider);
                                }
                            }
                            _logger.LogWarning("Provider {Provider} failed for word '{WordText}' (WC ID: {WCId}): {Error}",
                                               agent.ApiProvider, wcRecord.WordText, wcRecord.Id, errorMsg);
                        }

                        if (successfulResult != null && usedAgent != null && !string.IsNullOrWhiteSpace(successfulResult.Markdown))
                        {
                            var newExplanation = new WordExplanation
                            {
                                WordCollectionId = wcRecord.Id,
                                WordText = wcRecord.WordText, // Denormalized
                                WordLanguage = wcRecord.Language, // Denormalized (should be SOURCE_WORD_LANGUAGE)
                                ExplanationLanguage = TARGET_EXPLANATION_LANGUAGE,
                                MarkdownExplanation = successfulResult.Markdown,
                                CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds(),
                                ProviderModelName = $"{usedAgent.ApiProvider}:{successfulResult.ModelName}"
                            };

                            try
                            {
                                // Using direct DB client insert for WordExplanation
                                var insertResult = await _dbClient.Insertable(newExplanation).ExecuteCommandAsync();
                                if (insertResult > 0)
                                {
                                    successfullyAdded++;
                                    _logger.LogDebug("Successfully added WordExplanation for WC ID: {WCId}, Text: {WordText}",
                                                     wcRecord.Id, wcRecord.WordText);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to insert WordExplanation for WC ID: {WCId}, Text: {WordText} (ExecuteCommandAsync returned 0)",
                                                     wcRecord.Id, wcRecord.WordText);
                                }
                            }
                            catch (Exception dbEx)
                            {
                                _logger.LogError(dbEx, "Database error inserting WordExplanation for WC ID: {WCId}, Text: {WordText}. Possible duplicate or other constraint violation?",
                                                 wcRecord.Id, wcRecord.WordText);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("All available providers failed for WordCollection ID: {WCId}, Text: {WordText}.",
                                             wcRecord.Id, wcRecord.WordText);
                        }
                    }
                    catch (Exception serviceEx)
                    {
                        _logger.LogError(serviceEx, "Error processing WordCollection ID: {WCId}, Text: {WordText}",
                                         wcRecord.Id, wcRecord.WordText);
                    }
                    // await Task.Delay(100); // Optional delay
                }
            } while (wordCollectionBatch.Any());

            _logger.LogInformation("FillWordExplanationsTable process finished. Total Processed: {TotalProcessed}, Successfully Added: {SuccessfullyAdded}, Skipped (Existing): {SkippedExisting}, Skipped (Wrong Lang): {SkippedWrongLang}",
                                 totalProcessed, successfullyAdded, skippedExisting, skippedWrongLanguage);
            return new SuccessfulResult<object>(new
            {
                TotalProcessed = totalProcessed,
                SuccessfullyAdded = successfullyAdded,
                SkippedExistingExplanation = skippedExisting,
                SkippedWrongSourceLanguage = skippedWrongLanguage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during FillWordExplanationsTable process.");
            return Fail($"An error occurred: {ex.Message}");
        }
    }
}
