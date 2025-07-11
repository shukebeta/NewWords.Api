using Api.Framework.Models;
using Api.Framework.Extensions;
using NewWords.Api.Entities;
using NewWords.Api.Repositories;
using SqlSugar;
using Api.Framework;
using LLM;
using LLM.Models;
using LLM.Services;
using NewWords.Api.Services.interfaces;
using System.Globalization;

namespace NewWords.Api.Services
{
    public class VocabularyService(
        ISqlSugarClient db,
        ILanguageService languageService,
        IConfigurationService configurationService,
        ILogger<VocabularyService> logger,
        IRepositoryBase<WordCollection> wordCollectionRepository,
        IRepositoryBase<WordExplanation> wordExplanationRepository,
        IRepositoryBase<QueryHistory> queryHistoryRepository,
        IUserWordRepository userWordRepository)
        : IVocabularyService
    {
        // Handles WordExplanation entities

        public async Task<PageData<WordExplanation>> GetUserWordsAsync(int userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedWords = await db.Queryable<WordExplanation>()
                .RightJoin<UserWord>((we, uw) => we.Id == uw.WordExplanationId)
                .Where((we, uw) => uw.UserId == userId)
                .OrderBy((we, uw) => uw.CreatedAt, OrderByType.Desc)
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
                var userWord = await _HandleUserWord(userId, explanation);

                await db.AsTenant().CommitTranAsync();

                // Override CreatedAt with user's timestamp (when they learned the word)
                explanation.CreatedAt = userWord.CreatedAt;
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
                await _DeleteUserWordWithCleanup(userWord);
            }
            else
            {
                logger.LogWarning($"UserWord not found for deletion - UserId: {userId}, WordExplanationId: {wordExplanationId}");
            }
        }

        public async Task<WordExplanation> RefreshUserWordExplanationAsync(long wordExplanationId)
        {
            // 1. Get current explanation
            var explanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we => we.Id == wordExplanationId);
            if (explanation == null)
            {
                logger.LogWarning($"Word explanation not found for refresh - WordExplanationId: {wordExplanationId}");
                throw new ArgumentException("Word explanation not found");
            }

            // 2. Get current first agent
            var firstAgent = configurationService.Agents.FirstOrDefault();
            if (firstAgent == null)
            {
                logger.LogError("No agents configured for word explanation refresh");
                throw new InvalidOperationException("No agents configured");
            }

            var currentFirstAgentName = $"{firstAgent.Provider}:{firstAgent.ModelName}";

            // 3. Compare with existing provider model
            if (currentFirstAgentName == explanation.ProviderModelName)
            {
                logger.LogInformation($"Word explanation already uses current AI model, no refresh needed - WordExplanationId: {wordExplanationId}");
                throw new InvalidOperationException("Word explanation is already up to date with the latest AI model");
            }

            // 4. Get language names for regeneration
            var learningLanguageName = configurationService.GetLanguageName(explanation.LearningLanguage);
            var explanationLanguageName = configurationService.GetLanguageName(explanation.ExplanationLanguage);

            if (learningLanguageName == null || explanationLanguageName == null)
            {
                logger.LogError($"Language names not found for refresh - LearningLanguage: {explanation.LearningLanguage}, ExplanationLanguage: {explanation.ExplanationLanguage}");
                throw new InvalidOperationException("Language names not found");
            }

            // 5. Generate new explanation
            var newExplanationResult = await languageService.GetMarkdownExplanationWithFallbackAsync(
                explanation.WordText, explanationLanguageName, learningLanguageName);

            // 6. Handle generation failure
            if (!newExplanationResult.IsSuccess || string.IsNullOrWhiteSpace(newExplanationResult.Markdown))
            {
                logger.LogWarning($"Failed to generate new explanation for word '{explanation.WordText}' - WordExplanationId: {wordExplanationId}, Error: {newExplanationResult.ErrorMessage}");
                return explanation; // Return original on failure
            }

            // 7. Update explanation
            explanation.MarkdownExplanation = newExplanationResult.Markdown;
            explanation.ProviderModelName = newExplanationResult.ModelName;
            explanation.CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds();

            // 8. Save changes
            await wordExplanationRepository.UpdateAsync(explanation);

            logger.LogInformation($"Successfully refreshed word explanation - WordExplanationId: {wordExplanationId}, Word: '{explanation.WordText}', NewProvider: {newExplanationResult.ModelName}");

            return explanation;
        }

        public async Task<IList<WordExplanation>> MemoriesAsync(int userId, string localTimezone)
        {
            var earliest = await userWordRepository.GetFirstOrDefaultAsync(uw => uw.UserId == userId, "CreatedAt");
            if (earliest == null) return new List<WordExplanation>();

            var memories = new List<WordExplanation>();
            var timestampList = _GetTimestamps(earliest.CreatedAt, localTimezone);

            foreach (var timestamp in timestampList)
            {
                var wordExplanation = await db.Queryable<WordExplanation>()
                    .RightJoin<UserWord>((we, uw) => we.Id == uw.WordExplanationId)
                    .Where((we, uw) => uw.UserId == userId && uw.CreatedAt >= timestamp && uw.CreatedAt < timestamp + 86400)
                    .OrderBy((we, uw) => uw.CreatedAt)
                    .Select((we, uw) => new WordExplanation()
                    {
                        CreatedAt = uw.CreatedAt,
                    }, true)
                    .FirstAsync();

                if (wordExplanation != null)
                {
                    memories.Add(wordExplanation);
                }
            }

            return memories;
        }

        public async Task<IList<WordExplanation>> MemoriesOnAsync(int userId, string localTimezone, string yyyyMMdd)
        {
            // Get the specified time zone
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(localTimezone);
            // Parse the date string to a DateTime object
            var dayStartTimestamp = DateTimeOffset.ParseExact(yyyyMMdd, "yyyyMMdd", CultureInfo.InvariantCulture)
                .GetDayStartTimestamp(timeZone);

            var words = await db.Queryable<WordExplanation>()
                .RightJoin<UserWord>((we, uw) => we.Id == uw.WordExplanationId)
                .Where((we, uw) => uw.UserId == userId && uw.CreatedAt >= dayStartTimestamp && uw.CreatedAt < dayStartTimestamp + 86400)
                .OrderBy((we, uw) => uw.CreatedAt)
                .Select((we, uw) => new WordExplanation()
                {
                    CreatedAt = uw.CreatedAt,
                }, true)
                .ToListAsync();

            return words;
        }

        private async Task<UserWord> _HandleUserWord(int userId, WordExplanation explanationToReturn)
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
                return newUserWord;
            }

            return userWord;
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

        private async Task _DeleteUserWordWithCleanup(UserWord userWord)
        {
            try
            {
                await db.AsTenant().BeginTranAsync();

                // Delete the user word
                await userWordRepository.DeleteAsync(userWord);

                // Perform cleanup check
                await _CleanupOrphanedRecords(userWord.WordExplanationId);

                await db.AsTenant().CommitTranAsync();
            }
            catch (Exception ex)
            {
                await db.AsTenant().RollbackTranAsync();
                logger.LogError(ex, $"Error in _DeleteUserWordWithCleanup for user {userWord.UserId}, wordExplanationId {userWord.WordExplanationId}: Rollback.");
                throw;
            }
        }

        private async Task _CleanupOrphanedRecords(long wordExplanationId)
        {
            const int cleanupThresholdMinutes = 5;

            // Get the word explanation
            var wordExplanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we => we.Id == wordExplanationId);
            if (wordExplanation == null)
            {
                return;
            }

            // Check if any other users still reference this word explanation
            var otherUserWordExists = await userWordRepository.GetFirstOrDefaultAsync(uw => uw.WordExplanationId == wordExplanationId);
            if (otherUserWordExists != null)
            {
                // Other users still use this explanation, don't delete
                return;
            }

            // Get the word collection
            var wordCollection = await wordCollectionRepository.GetFirstOrDefaultAsync(wc => wc.Id == wordExplanation.WordCollectionId);
            if (wordCollection == null)
            {
                // Word collection doesn't exist, just delete the explanation
                await wordExplanationRepository.DeleteAsync(wordExplanation);
                logger.LogInformation($"Cleaned up orphaned word explanation: {wordExplanation.WordText} (ID: {wordExplanationId})");
                return;
            }

            // Check if both records were created within the threshold time
            var timeDifferenceSeconds = Math.Abs(wordExplanation.CreatedAt - wordCollection.CreatedAt);
            var timeDifferenceMinutes = timeDifferenceSeconds / 60.0;

            if (timeDifferenceMinutes <= cleanupThresholdMinutes)
            {
                // Check if any other word explanations reference this word collection
                var otherExplanationExists = await wordExplanationRepository.GetFirstOrDefaultAsync(we =>
                    we.WordCollectionId == wordCollection.Id && we.Id != wordExplanationId);

                if (otherExplanationExists == null)
                {
                    // No other explanations reference this collection, safe to delete both
                    await wordExplanationRepository.DeleteAsync(wordExplanation);
                    await _CleanupQueryHistory(wordCollection.Id);
                    await wordCollectionRepository.DeleteAsync(wordCollection);
                    logger.LogInformation($"Cleaned up orphaned word collection, explanation, and query history: {wordCollection.WordText} (Collection ID: {wordCollection.Id}, Explanation ID: {wordExplanationId})");
                }
                else
                {
                    // Other explanations exist for this collection, only delete the explanation
                    await wordExplanationRepository.DeleteAsync(wordExplanation);
                    logger.LogInformation($"Cleaned up orphaned word explanation: {wordExplanation.WordText} (ID: {wordExplanationId})");
                }
            }
        }

        private async Task _CleanupQueryHistory(long wordCollectionId)
        {
            var deletedCount = await queryHistoryRepository.DeleteReturnRowsAsync(qh => qh.WordCollectionId == wordCollectionId);

            if (deletedCount > 0)
            {
                logger.LogInformation($"Cleaned up {deletedCount} query history records for word collection ID: {wordCollectionId}");
            }
        }

        private static long[] _GetTimestamps(long initialUnixTimestamp, string timeZoneId)
        {
            var targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var startDate = initialUnixTimestamp.ToDateTimeOffset(targetTimeZone);
            var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, targetTimeZone);

            var timestamps = new List<long>();

            // Add spaced repetition intervals optimized for vocabulary learning
            var dayIntervals = new[] { 3, 7, 14, 30, 60, 90, 180, 365 }; // 3 days, 1 week, 2 weeks, 1 month, 2 months, 3 months, 6 months, 1 year

            foreach (var daysAgo in dayIntervals)
            {
                var targetDate = today.AddDays(-daysAgo);

                // Only include if the target date is after the user started using the app
                if (targetDate >= startDate)
                {
                    timestamps.Add(targetDate.GetDayStartTimestamp(targetTimeZone));
                }
            }

            // Always add yesterday and today
            timestamps.Add(today.AddDays(-1).GetDayStartTimestamp(targetTimeZone));
            timestamps.Add(today.GetDayStartTimestamp(targetTimeZone));

            return timestamps.OrderBy(t => t).ToArray();
        }
    }
}
