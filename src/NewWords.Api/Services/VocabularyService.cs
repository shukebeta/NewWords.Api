using Api.Framework.Models;
using Api.Framework.Extensions;
using NewWords.Api.Entities;
using NewWords.Api.Repositories;
using SqlSugar;
using Api.Framework;
using LLM;
using LLM.Models;
using LLM.Services;

namespace NewWords.Api.Services
{
    public class VocabularyService(
        ISqlSugarClient db,
        ILanguageService languageService,
        IConfigurationService configurationService,
        ILogger<VocabularyService> logger,
        IRepositoryBase<WordCollection> wordCollectionRepository,
        IRepositoryBase<WordExplanation> wordExplanationRepository,
        IUserWordRepository userWordRepository)
        : IVocabularyService
    {
        // Handles WordExplanation entities

        public async Task<PageData<WordExplanation>> GetUserWordsAsync(int userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedWords = await db.Queryable<WordExplanation>()
                .RightJoin<UserWord>((we, uw) => we.Id == uw.WordExplanationId)
                .Where((we,uw) => uw.UserId == userId)
                .OrderBy((we,uw) => uw.CreatedAt, OrderByType.Desc)
                .Select((we, uw) => new WordExplanation()
                {
                    CreatedAt = uw.CreatedAt,
                }, true)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<WordExplanation>
            {
                DataList = pagedWords,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<WordExplanation> AddUserWordAsync(int userId, string wordText, string learningLanguageCode, string explanationLanguageCode)
        {
            try
            {
                await db.AsTenant().BeginTranAsync();
                // 1. Handle WordCollection
                var wordCollectionId = await _HandleWordCollection(wordText);

                // 2. Handle WordExplanation (Explanation Cache)
                var explanation = await _HandleExplanation(wordText, learningLanguageCode, explanationLanguageCode, wordCollectionId);

                // 3. Handle UserWord
                await _HandleUserWord(userId, explanation);

                await db.AsTenant().CommitTranAsync();
                return explanation;
            }
            catch (Exception ex) // Catch exceptions from UseTranAsync or input validation
            {
                await db.AsTenant().RollbackTranAsync();
                logger.LogError(ex, $"Error in AddUserWordAsync for user {userId}, word '{wordText}': Rollback.");
                throw;
            }
        }

        public async Task DelUserWordAsync(int userId, long wordExplanationId)
        {
            var userWord = await userWordRepository.GetFirstOrDefaultAsync(uw =>
                uw.UserId == userId && uw.WordExplanationId == wordExplanationId);

            if (userWord != null)
            {
                await userWordRepository.DeleteAsync(userWord);
            }
            else
            {
                logger.LogWarning($"UserWord not found for deletion - UserId: {userId}, WordExplanationId: {wordExplanationId}");
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
        }

        private async Task<WordExplanation> _HandleExplanation(string wordText, string learningLanguageCode, string explanationLanguageCode,
            long wordCollectionId)
        {
            var explanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we =>
                we.WordCollectionId == wordCollectionId && 
                we.LearningLanguage == learningLanguageCode && 
                we.ExplanationLanguage == explanationLanguageCode);

            if (explanation is not null) return explanation;
            var learningLanguageName = configurationService.GetLanguageName(learningLanguageCode)!;
            var explanationLanguageName = configurationService.GetLanguageName(explanationLanguageCode)!;
            var explanationResult =
                await languageService.GetMarkdownExplanationWithFallbackAsync(wordText, explanationLanguageName, learningLanguageName);

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
                LearningLanguage = learningLanguageCode,
                ExplanationLanguage = explanationLanguageCode,
                MarkdownExplanation = explanationResult.Markdown,
                ProviderModelName = explanationResult.ModelName,
                CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds(),
            };
            var newExplanationId = await wordExplanationRepository.InsertReturnIdentityAsync(newExplanation);
            newExplanation.Id = newExplanationId; // Assign the returned ID to the entity's Id property
            explanation = newExplanation;

            return explanation;
        }

        private async Task<long> _HandleWordCollection(string wordText)
        {
            var currentTime = DateTime.UtcNow.ToUnixTimeSeconds();
            wordText = wordText.Trim();
            
            // Try to find existing word (language-agnostic)
            var existingWord = await wordCollectionRepository.GetFirstOrDefaultAsync(wc =>
                wc.WordText == wordText);

            if (existingWord == null)
            {
                // Create new word record
                return await _AddWordCollection(wordText, currentTime);
            }
            
            // Update existing word
            existingWord.QueryCount++;
            existingWord.UpdatedAt = currentTime;
            await wordCollectionRepository.UpdateAsync(existingWord);
            return existingWord.Id;
        }

        private async Task<long> _AddWordCollection(string wordText, long currentTime)
        {
            var newCollectionWord = new WordCollection
            {
                WordText = wordText,
                QueryCount = 1,
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };
            return await wordCollectionRepository.InsertReturnIdentityAsync(newCollectionWord);
        }
    }
}
