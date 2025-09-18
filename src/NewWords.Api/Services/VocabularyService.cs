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
using System.Diagnostics;

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
            var overallStopwatch = Stopwatch.StartNew();
            logger.LogInformation("Starting AddUserWordAsync for user {UserId}, word '{WordText}', learning: {LearningLanguage}, explanation: {ExplanationLanguage}", 
                userId, wordText, learningLanguageCode, explanationLanguageCode);
            
            try
            {
                var stepStopwatch = Stopwatch.StartNew();
                
                // Normalize input early
                var wordTextTrimmed = NormalizeWord(wordText);
                var learningLanguageName = configurationService.GetLanguageName(learningLanguageCode)!;
                var explanationLanguageName = configurationService.GetLanguageName(explanationLanguageCode)!;
                
                stepStopwatch.Stop();

                // 2. Then check if there is a local explanation (original word)
                stepStopwatch.Restart();
                var localWord = await wordCollectionRepository.GetFirstOrDefaultAsync(wc => wc.WordText == wordTextTrimmed && wc.DeletedAt == null);
                stepStopwatch.Stop();
                
                WordExplanation? explanation = null;
                long wordCollectionId = 0;
                string canonicalWord = wordTextTrimmed;

                if (localWord != null)
                {
                    stepStopwatch.Restart();
                    explanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we =>
                        we.WordCollectionId == localWord.Id &&
                        we.LearningLanguage == learningLanguageCode &&
                        we.ExplanationLanguage == explanationLanguageCode);
                    stepStopwatch.Stop();
                    
                    if (explanation != null)
                    {
                        wordCollectionId = localWord.Id;
                    }
                }
                
                // 3. 如果本地没有解释，调用AI（外部调用放在事务之外），提取标准词
                ExplanationResult? aiResult = null;
                if (explanation == null)
                {
                    logger.LogInformation("No local explanation found for word '{WordText}', calling AI service", wordTextTrimmed);
                    
                    stepStopwatch.Restart();
                    try
                    {
                        aiResult = await CallAiWithRetryAsync(wordTextTrimmed, explanationLanguageName, learningLanguageName);
                        stepStopwatch.Stop();
                        logger.LogInformation("AI call completed in {ElapsedMs}ms for word '{WordText}', success: {Success}", 
                            stepStopwatch.ElapsedMilliseconds, wordTextTrimmed, aiResult?.IsSuccess ?? false);
                    }
                    catch (Exception ex)
                    {
                        stepStopwatch.Stop();
                        logger.LogWarning(ex, "AI call failed after {ElapsedMs}ms for word '{WordText}'; will fallback to user input as canonical word", 
                            stepStopwatch.ElapsedMilliseconds, wordTextTrimmed);
                        aiResult = new ExplanationResult { IsSuccess = false, ErrorMessage = ex.Message };
                    }

                    if (aiResult == null || !aiResult.IsSuccess || string.IsNullOrWhiteSpace(aiResult.Markdown))
                    {
                        // Do not throw here; fall back to user input to avoid failing whole transaction
                        logger.LogWarning("AI explanation unavailable for '{WordText}': {ErrorMessage}. Falling back to original word", 
                            wordTextTrimmed, aiResult?.ErrorMessage ?? "empty response");
                        canonicalWord = wordTextTrimmed;
                    }
                    else
                    {
                        stepStopwatch.Restart();
                        canonicalWord = ExtractCanonicalWordFromMarkdown(aiResult.Markdown);
                        if (string.IsNullOrWhiteSpace(canonicalWord)) canonicalWord = wordTextTrimmed;
                        stepStopwatch.Stop();
                    }

                    // Now start a short transaction to update/read DB state
                    stepStopwatch.Restart();
                    await db.AsTenant().BeginTranAsync();
                    try
                    {
                        var handleWordCollectionStart = Stopwatch.StartNew();
                        wordCollectionId = await _HandleWordCollection(wordTextTrimmed, canonicalWord);
                        handleWordCollectionStart.Stop();
                        
                        var handleExplanationStart = Stopwatch.StartNew();
                        explanation = await _HandleExplanation(canonicalWord, learningLanguageCode, explanationLanguageCode, wordCollectionId, aiResult);
                        handleExplanationStart.Stop();
                        
                        await db.AsTenant().CommitTranAsync();
                        stepStopwatch.Stop();
                    }
                    catch
                    {
                        await db.AsTenant().RollbackTranAsync();
                        stepStopwatch.Stop();
                        logger.LogError("Database transaction failed and rolled back after {ElapsedMs}ms for word '{WordText}'", 
                            stepStopwatch.ElapsedMilliseconds, wordTextTrimmed);
                        throw;
                    }
                }

                // 4. Handle UserWord
                stepStopwatch.Restart();
                var userWord = await _HandleUserWord(userId, explanation);
                stepStopwatch.Stop();

                // Nothing to do here; commit/rollback handled where transaction was opened.

                // Override CreatedAt with user's timestamp (when they learned the word)
                explanation.CreatedAt = userWord.CreatedAt;
                
                overallStopwatch.Stop();
                logger.LogInformation("AddUserWordAsync completed successfully in {ElapsedMs}ms for user {UserId}, word '{WordText}'", 
                    overallStopwatch.ElapsedMilliseconds, userId, wordTextTrimmed);
                
                return explanation;
            }
            catch (Exception ex) // Catch exceptions from UseTranAsync or input validation
            {
                overallStopwatch.Stop();
                await db.AsTenant().RollbackTranAsync();
                logger.LogError(ex, "AddUserWordAsync failed after {ElapsedMs}ms for user {UserId}, word '{WordText}': Rollback executed", 
                    overallStopwatch.ElapsedMilliseconds, userId, wordText);
                throw;
            }
        }

    /// <summary>
    /// Extract the canonical word from the first line of AI markdown (e.g. "**apple**" -> "apple")
    /// </summary>
    internal string ExtractCanonicalWordFromMarkdown(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
            var firstLine = markdown.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => !string.IsNullOrEmpty(l));
            if (!string.IsNullOrEmpty(firstLine))
            {
                // Prefer the first bold block **...** on the first non-empty line
                var start = firstLine.IndexOf("**", StringComparison.Ordinal);
                if (start >= 0)
                {
                    var end = firstLine.IndexOf("**", start + 2, StringComparison.Ordinal);
                    if (end > start + 1)
                    {
                        var candidate = firstLine.Substring(start + 2, end - (start + 2)).Trim();
                        if (!string.IsNullOrEmpty(candidate)) return candidate;
                    }
                }

                // Fallback: strip basic markdown characters and take the first token
                var cleaned = firstLine.Replace("**", "").Replace("*", "").Replace("`", "").Replace("#", "").Trim();
                var tokens = cleaned.Split(new[] { ' ', '\t', '-', '—', '\u2014', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0) return tokens[0].Trim();
            }

            // As a last resort, return trimmed whole markdown
            return markdown.Trim();
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
            long wordCollectionId, ExplanationResult? aiResult)
        {
            var explanation = await wordExplanationRepository.GetFirstOrDefaultAsync(we =>
                we.WordCollectionId == wordCollectionId &&
                we.LearningLanguage == learningLanguageCode &&
                we.ExplanationLanguage == explanationLanguageCode);

            if (explanation is not null) return explanation;

            // Use the provided AI result instead of calling AI again
            if (aiResult == null || !aiResult.IsSuccess || string.IsNullOrWhiteSpace(aiResult.Markdown))
            {
                logger.LogError("AI result is null or failed for word '{WordText}': {ErrorMessage}", 
                    wordText, aiResult?.ErrorMessage ?? "null result");
                throw new Exception($"Failed to get explanation from AI: {aiResult?.ErrorMessage ?? "AI result is null"}");
            }

            logger.LogInformation("Reusing AI result for word '{WordText}' from provider {Provider}", 
                wordText, aiResult.ModelName);

            var newExplanation = new WordExplanation
            {
                WordCollectionId = wordCollectionId,
                WordText = wordText,
                LearningLanguage = learningLanguageCode,
                ExplanationLanguage = explanationLanguageCode,
                MarkdownExplanation = aiResult.Markdown,
                ProviderModelName = aiResult.ModelName,
                CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds(),
            };
            var newExplanationId = await wordExplanationRepository.InsertReturnIdentityAsync(newExplanation);
            newExplanation.Id = newExplanationId;
            return newExplanation;
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

        /// <summary>
        /// 处理单词规范化：如果AI纠正了用户输入，确保WordCollection只保留标准词。
        /// </summary>
        /// <param name="userInput">用户原始输入</param>
        /// <param name="canonicalWord">AI返回的标准词</param>
        /// <returns>标准词的WordCollection.Id</returns>
        private async Task<long> EnsureCanonicalWordAsync(string userInput, string canonicalWord)
        {
            var currentTime = DateTime.UtcNow.ToUnixTimeSeconds();
            userInput = userInput.Trim();
            canonicalWord = canonicalWord.Trim();

            // 查找错词和标准词
            var wrongEntry = await wordCollectionRepository.GetFirstOrDefaultAsync(wc => wc.WordText == userInput && wc.DeletedAt == null);
            var correctEntry = await wordCollectionRepository.GetFirstOrDefaultAsync(wc => wc.WordText == canonicalWord && wc.DeletedAt == null);

            if (correctEntry != null)
            {
                // Canonical word exists, soft-delete the typo entry
                if (wrongEntry != null && wrongEntry.WordText != canonicalWord)
                {
                    wrongEntry.DeletedAt = currentTime;
                    await wordCollectionRepository.UpdateAsync(wrongEntry);
                }
                return correctEntry.Id;
            }
            else if (wrongEntry != null && wrongEntry.WordText != canonicalWord)
            {
                // Canonical word does not exist, update typo entry to canonical word
                wrongEntry.WordText = canonicalWord;
                wrongEntry.UpdatedAt = currentTime;
                await wordCollectionRepository.UpdateAsync(wrongEntry);
                return wrongEntry.Id;
            }
            else if (wrongEntry != null)
            {
                // User input is already the canonical word
                wrongEntry.QueryCount++;
                wrongEntry.UpdatedAt = currentTime;
                await wordCollectionRepository.UpdateAsync(wrongEntry);
                return wrongEntry.Id;
            }
            else
            {
                // 两者都不存在，插入标准词
                return await _AddWordCollection(canonicalWord, currentTime);
            }
        }

        // Modified original _HandleWordCollection, added canonicalWord parameter
        private async Task<long> _HandleWordCollection(string userInput, string? canonicalWord = null)
        {
            // If canonicalWord is not specified, default to userInput
            canonicalWord ??= userInput;
            return await EnsureCanonicalWordAsync(userInput, canonicalWord);
        }

        /// <summary>
        /// Normalize word text for consistent comparisons and storage
        /// </summary>
        private static string NormalizeWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return string.Empty;
            return word.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Call AI with limited retries and return ExplanationResult. This wraps the ILanguageService call.
        /// </summary>
        private async Task<ExplanationResult> CallAiWithRetryAsync(string inputText, string nativeLanguageName, string targetLanguageName)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogInformation("Starting AI call for word '{InputText}', native: {NativeLanguage}, target: {TargetLanguage}", 
                inputText, nativeLanguageName, targetLanguageName);
            
            try
            {
                var result = await languageService.GetMarkdownExplanationWithFallbackAsync(inputText, nativeLanguageName, targetLanguageName);
                stopwatch.Stop();
                
                if (result.IsSuccess)
                {
                    logger.LogInformation("AI call succeeded in {ElapsedMs}ms for word '{InputText}', provider: {ProviderModel}", 
                        stopwatch.ElapsedMilliseconds, inputText, result.ModelName);
                }
                else
                {
                    logger.LogWarning("AI call failed in {ElapsedMs}ms for word '{InputText}', error: {ErrorMessage}", 
                        stopwatch.ElapsedMilliseconds, inputText, result.ErrorMessage);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogWarning(ex, "AI call threw exception after {ElapsedMs}ms for word '{InputText}'", 
                    stopwatch.ElapsedMilliseconds, inputText);
                return new ExplanationResult { IsSuccess = false, Markdown = string.Empty, ErrorMessage = ex.Message };
            }
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
            var dayIntervals = new[] { 0, 1, 3, 7, 14, 30, 60, 90, 180, 365 }; // today, yesterday, 3 days, 1 week, 2 weeks, 1 month, 2 months, 3 months, 6 months, 1 year

            foreach (var daysAgo in dayIntervals)
            {
                var targetDate = today.AddDays(-daysAgo);

                // Only include if the target date is after the user started using the app
                if (targetDate >= startDate)
                {
                    timestamps.Add(targetDate.GetDayStartTimestamp(targetTimeZone));
                }
            }

            // Note: today and yesterday are now included in dayIntervals with proper filtering

            return timestamps.OrderBy(t => t).ToArray();
        }
    }
}
