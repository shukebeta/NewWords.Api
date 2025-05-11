using Api.Framework.Models;
using Api.Framework.Extensions; // For ToUnixTimeSeconds
using NewWords.Api.Entities;
using SqlSugar;
using LLM.Services; // For TranslationAndExplanationService
using LLM.Configuration; // For LlmConfigurationService and AgentConfig
using Microsoft.Extensions.Logging; // Added ILogger
using System; // Added for Exception and ArgumentException
using System.Linq; // Added for Linq operations like .First() and .Any()
using System.Threading.Tasks; // Added for Task

namespace NewWords.Api.Services
{
    public class VocabularyService : IVocabularyService
    {
        private readonly ISqlSugarClient _db;
        private readonly IUserService _userService; // Kept for potential future use, though not in current AddUserWordAsync happy path
        private readonly TranslationAndExplanationService _translationAndExplanationService;
        private readonly LlmConfigurationService _llmConfigurationService;
        private readonly ILogger<VocabularyService> _logger;

        public VocabularyService(
            ISqlSugarClient db,
            IUserService userService,
            TranslationAndExplanationService translationAndExplanationService,
            LlmConfigurationService llmConfigurationService,
            ILogger<VocabularyService> logger)
        {
            _db = db;
            _userService = userService;
            _translationAndExplanationService = translationAndExplanationService;
            _llmConfigurationService = llmConfigurationService;
            _logger = logger;
        }

        public async Task<PageData<WordExplanation>> GetUserWordsAsync(long userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedWords = await _db.Queryable<UserWordEntity>()
                .LeftJoin<WordExplanation>((uwe, we) => uwe.WordExplanationId == we.WordExplanationId)
                .Where(uwe => uwe.UserId == userId)
                .OrderBy(uwe => uwe.CreatedAt, OrderByType.Desc)
                .Select((uwe, we) => we)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<WordExplanation>
            {
                DataList = pagedWords,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<WordExplanation> AddUserWordAsync(long userId, string wordText, string wordLanguage, string explanationLanguage)
        {
            // Basic input validation (can be expanded)
            if (string.IsNullOrWhiteSpace(wordText))
                throw new ArgumentException("Word text cannot be empty.", nameof(wordText));
            if (string.IsNullOrWhiteSpace(wordLanguage))
                throw new ArgumentException("Word language cannot be empty.", nameof(wordLanguage));
            if (string.IsNullOrWhiteSpace(explanationLanguage))
                throw new ArgumentException("Explanation language cannot be empty.", nameof(explanationLanguage));

            WordExplanation explanationToReturn;

            try
            {
                await _db.Ado.BeginTranAsync();

                // 1. Handle WordCollection
                var wordInCollection = await _db.Queryable<WordCollection>()
                    .Where(wc => wc.WordText == wordText && wc.Language == wordLanguage && wc.DeletedAt == null)
                    .SingleAsync();

                long wordCollectionId;
                long currentTime = DateTime.UtcNow.ToUnixTimeSeconds();

                if (wordInCollection != null)
                {
                    wordCollectionId = wordInCollection.Id;
                    await _db.Updateable<WordCollection>()
                        .SetColumns(it => new WordCollection { QueryCount = it.QueryCount + 1, UpdatedAt = currentTime })
                        .Where(it => it.Id == wordCollectionId)
                        .ExecuteCommandAsync();
                }
                else
                {
                    var newCollectionWord = new WordCollection
                    {
                        WordText = wordText,
                        Language = wordLanguage,
                        QueryCount = 1,
                        CreatedAt = currentTime,
                        UpdatedAt = currentTime // Set UpdatedAt on creation as well
                    };
                    wordCollectionId = await _db.Insertable(newCollectionWord).ExecuteReturnBigIdentityAsync();
                }

                // 2. Handle WordExplanation (Explanation Cache)
                explanationToReturn = await _db.Queryable<WordExplanation>()
                    .Where(we => we.WordCollectionId == wordCollectionId && we.ExplanationLanguage == explanationLanguage)
                    .SingleAsync();

                if (explanationToReturn == null)
                {
                    // No cached explanation, call AI Service
                    var agentConfigs = _llmConfigurationService.GetAgentConfigs();
                    if (agentConfigs == null || !agentConfigs.Any())
                    {
                        await _db.Ado.RollbackTranAsync();
                        _logger.LogError("No LLM agents configured.");
                        throw new InvalidOperationException("No LLM agents configured. Cannot fetch word explanation.");
                    }
                    var agentConfig = agentConfigs.First(); // Happy path: use the first agent

                    var explanationResult = await _translationAndExplanationService.GetMarkdownExplanationAsync(wordText, explanationLanguage, agentConfig);

                    if (!explanationResult.IsSuccess || string.IsNullOrWhiteSpace(explanationResult.Markdown))
                    {
                        await _db.Ado.RollbackTranAsync();
                        _logger.LogError($"Failed to get explanation from AI for word '{wordText}': {explanationResult.ErrorMessage}");
                        throw new Exception($"Failed to get explanation from AI: {explanationResult.ErrorMessage ?? "AI returned empty markdown."}");
                    }

                    var newExplanation = new WordExplanation
                    {
                        WordCollectionId = wordCollectionId,
                        WordText = wordText, // Denormalized
                        WordLanguage = wordLanguage, // Denormalized
                        ExplanationLanguage = explanationLanguage,
                        MarkdownExplanation = explanationResult.Markdown,
                        ProviderModelName = explanationResult.ModelName,
                        CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds()
                    };
                    var newExplanationId = await _db.Insertable(newExplanation).ExecuteReturnBigIdentityAsync();
                    newExplanation.WordExplanationId = newExplanationId;
                    explanationToReturn = newExplanation;
                }

                // 3. Handle UserWord Link
                var userWordLink = await _db.Queryable<UserWordEntity>()
                    .Where(uw => uw.UserId == userId && uw.WordExplanationId == explanationToReturn.WordExplanationId)
                    .SingleAsync();

                if (userWordLink == null)
                {
                    var newUserWord = new UserWordEntity
                    {
                        UserId = userId,
                        WordExplanationId = explanationToReturn.WordExplanationId,
                        Status = 0, // Default status
                        CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds()
                    };
                    await _db.Insertable(newUserWord).ExecuteCommandAsync();
                }

                await _db.Ado.CommitTranAsync();
                return explanationToReturn;
            }
            catch (Exception ex)
            {
                await _db.Ado.RollbackTranAsync();
                _logger.LogError(ex, $"Error in AddUserWordAsync for user {userId}, word '{wordText}'.");
                throw; // Re-throw the exception to be handled by global error handler or controller
            }
        }
    }
}
