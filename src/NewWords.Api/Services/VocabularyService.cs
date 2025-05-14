using Api.Framework.Models;
using Api.Framework.Extensions; // For ToUnixTimeSeconds
using NewWords.Api.Entities;
using NewWords.Api.Repositories; // Added for repository interfaces
using SqlSugar;
using LLM.Services; // For TranslationAndExplanationService
using LLM.Configuration; // For LlmConfigurationService and AgentConfig
using Microsoft.Extensions.Logging; // Added ILogger
using System; // Added for Exception and ArgumentException
using System.Linq; // Added for Linq operations like .First() and .Any()
using System.Threading.Tasks; // Added for Task

namespace NewWords.Api.Services
{
    public class VocabularyService(
        ISqlSugarClient db,
        TranslationAndExplanationService translationAndExplanationService,
        LlmConfigurationService llmConfigurationService,
        ILogger<VocabularyService> logger,
        IWordCollectionRepository wordCollectionRepository,
        IWordRepository wordExplanationRepository,
        IUserWordRepository userWordRepository)
        : IVocabularyService
    {
        // Handles WordExplanation entities

        public async Task<PageData<WordExplanation>> GetUserWordsAsync(int userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            // Using _db directly for complex query; repositories are for simpler CRUD in this context.
            var pagedWords = await db.Queryable<UserWord>()
                .LeftJoin<WordExplanation>((uwe, we) => uwe.WordExplanationId == we.Id)
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

        public async Task<WordExplanation> AddUserWordAsync(int userId, string wordText, string wordLanguage, string explanationLanguage)
        {
            try
            {
                await db.AsTenant().BeginTranAsync();
                // 1. Handle WordCollection
                var wordCollectionId = await _HandleWordCollection(wordText, wordLanguage);
                // 2. Handle WordExplanation (Explanation Cache)
                var explanation = await _HandleExplanation(wordText, wordLanguage, explanationLanguage, wordCollectionId);

                // 3. Handle UserWord
                await _HandleUserWord(userId, explanation);

                return explanation!;
            }
            catch (Exception ex) // Catch exceptions from UseTranAsync or input validation
            {
                await db.AsTenant().RollbackTranAsync();
                logger.LogError(ex, $"Error in AddUserWordAsync for user {userId}, word '{wordText}': Rollback.");
                throw;
            }
        }

        private async Task _HandleUserWord(int userId, WordExplanation explanationToReturn)
        {
            var userWord = await userWordRepository.GetFirstOrDefaultAsync(uw =>
                uw.UserId == userId && uw.WordExplanationId == explanationToReturn.Id);

            if (userWord == null)
            {
                var newUserWord = new UserWord
                {
                    UserId = userId,
                    WordExplanationId = explanationToReturn.Id,
                    Status = 0,
                    CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds()
                };
                await userWordRepository.InsertAsync(newUserWord);
            }
            await db.AsTenant().CommitTranAsync();
        }

        private async Task<WordExplanation> _HandleExplanation(string wordText, string wordLanguage, string explanationLanguage,
            long wordCollectionId)
        {
            var explanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we =>
                we.WordCollectionId == wordCollectionId && we.ExplanationLanguage == explanationLanguage);

            if (explanation == null)
            {
                var agentConfigs = llmConfigurationService.GetAgentConfigs();
                if (agentConfigs == null || !agentConfigs.Any())
                {
                    logger.LogError("No LLM agents configured.");
                    throw new InvalidOperationException("No LLM agents configured. Cannot fetch word explanation.");
                }

                var agentConfig = agentConfigs.First();

                var explanationResult =
                    await translationAndExplanationService.GetMarkdownExplanationAsync(wordText,
                        explanationLanguage, agentConfig);

                if (!explanationResult.IsSuccess || string.IsNullOrWhiteSpace(explanationResult.Markdown))
                {
                    logger.LogError(
                        $"Failed to get explanation from AI for word '{wordText}': {explanationResult.ErrorMessage}");
                    throw new Exception(
                        $"Failed to get explanation from AI: {explanationResult.ErrorMessage ?? "AI returned empty markdown."}");
                }

                var newExplanation = new WordExplanation
                {
                    WordCollectionId = wordCollectionId,
                    WordText = wordText,
                    WordLanguage = wordLanguage,
                    ExplanationLanguage = explanationLanguage,
                    MarkdownExplanation = explanationResult.Markdown,
                    ProviderModelName = explanationResult.ModelName,
                    CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds()
                };
                var newExplanationId = await wordExplanationRepository.InsertReturnIdentityAsync(newExplanation);
                newExplanation.Id = newExplanationId; // Assign the returned ID to the entity's Id property
                explanation = newExplanation;
            }

            return explanation;
        }

        private async Task<long> _HandleWordCollection(string wordText, string wordLanguage)
        {
            var wordInCollection = await wordCollectionRepository.GetFirstOrDefaultAsync(wc =>
                wc.WordText == wordText.Trim() && wc.Language == wordLanguage && wc.DeletedAt == null);
            long wordCollectionId;
            long currentTime = DateTime.UtcNow.ToUnixTimeSeconds();

            if (wordInCollection != null)
            {
                wordInCollection.QueryCount++;
                wordInCollection.UpdatedAt = currentTime;
                await wordCollectionRepository.UpdateAsync(wordInCollection);
                wordCollectionId = wordInCollection.Id;
            }
            else
            {
                var newCollectionWord = new WordCollection
                {
                    WordText = wordText,
                    Language = wordLanguage,
                    QueryCount = 1,
                    CreatedAt = currentTime,
                    UpdatedAt = currentTime
                };
                wordCollectionId = await wordCollectionRepository.InsertReturnIdentityAsync(newCollectionWord);
            }

            return wordCollectionId;
        }
    }
}
