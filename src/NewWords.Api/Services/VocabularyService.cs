using NewWords.Api.Models.DTOs.Vocabulary;
using NewWords.Api.Entities;
using NewWords.Api.Enums;
using SqlSugar;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Required for .Select() and potentially other LINQ methods
using System; // Required for DateTime

namespace NewWords.Api.Services
{
    public class VocabularyService(
        Repositories.IUserRepository userRepository,
        Repositories.IWordRepository wordRepository,
        Repositories.IUserWordRepository userWordRepository)
        : IVocabularyService
    {
        // TODO: Inject a background job client service (e.g., IBackgroundJobClient from Hangfire) later

        public async Task<UserWordDto?> AddWordAsync(long userId, AddWordRequestDto addWordDto)
        {
            // 1. Get user details (needed for languages)
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return null; // User not found
            }

            // 2. Normalize word text (e.g., lowercase, trim)
            var wordText = addWordDto.WordText.Trim().ToLowerInvariant(); // Example normalization
            if (string.IsNullOrEmpty(wordText))
            {
                return null; // Or throw validation exception
            }

            var wordLanguage = user.CurrentLearningLanguage;
            var userNativeLanguage = user.NativeLanguage;

            // 3. Find or Create the canonical Word entry
            var wordEntry = await wordRepository.GetByTextAndLanguageAsync(wordText, wordLanguage, userNativeLanguage);

            bool needsGeneration = false;
            if (wordEntry == null)
            {
                // Create new Word entry
                wordEntry = new Word
                {
                    WordText = wordText,
                    WordLanguage = wordLanguage,
                    ExplanationLanguage = userNativeLanguage,
                    // LLM fields are initially null
                    CreatedAt = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
                };
                await wordRepository.InsertAsync(wordEntry);
                needsGeneration = true;
            }
            else if (string.IsNullOrEmpty(wordEntry.Definitions) && string.IsNullOrEmpty(wordEntry.Examples) && string.IsNullOrEmpty(wordEntry.Pronunciation))
            {
                // Word exists but LLM data hasn't been generated yet (or failed previously)
                needsGeneration = true; // Potentially re-trigger generation
            }


            // 4. Find or Create the UserWord link
            var userWordEntry = await userWordRepository.GetByUserAndWordIdAsync(userId, wordEntry.WordId);

            if (userWordEntry == null)
            {
                userWordEntry = new UserWord
                {
                    UserId = userId,
                    WordId = wordEntry.WordId,
                    Status = WordStatus.New,
                    CreatedAt = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
                };
                await userWordRepository.InsertAsync(userWordEntry);
            }
            // else: User already has this word, just return the existing entry details

            // 5. Trigger background generation if needed
            if (needsGeneration && wordEntry.WordId > 0)
            {
                // TODO: Enqueue background job here
                // Example: _backgroundJobClient.Enqueue<ILlmGenerationService>(service => service.GenerateWordDataAsync(wordEntry.WordId));
                Console.WriteLine($"Placeholder: Trigger LLM generation for WordId: {wordEntry.WordId}");
            }

            // 6. Map to DTO and return
            return MapToUserWordDto(userWordEntry, wordEntry);
        }

        public async Task<List<UserWordDto>> GetUserWordsAsync(long userId, WordStatus? status, int page, int pageSize)
        {
            var userWords = await userWordRepository.GetUserWordsAsync(userId, status, page, pageSize);

            var wordIds = userWords.Select(uw => uw.WordId).ToList();
            var words = await wordRepository.GetListByIdsAsync(wordIds.ToArray());

            var wordDict = words.ToDictionary(w => w.WordId, w => w);
            var results = new List<UserWordDto>();
            foreach (var uw in userWords)
            {
                if (wordDict.TryGetValue(uw.WordId, out var word))
                {
                    results.Add(new UserWordDto
                    {
                        UserWordId = uw.UserWordId,
                        WordId = uw.WordId,
                        Status = uw.Status,
                        AddedAt = DateTime.UnixEpoch.AddSeconds(uw.CreatedAt),
                        WordText = word.WordText,
                        WordLanguage = word.WordLanguage,
                        UserNativeLanguage = word.ExplanationLanguage,
                        Pronunciation = word.Pronunciation,
                        Definitions = word.Definitions,
                        Examples = word.Examples,
                        GeneratedAt = word.CreatedAt > 0 ? DateTime.UnixEpoch.AddSeconds(word.CreatedAt) : null
                    });
                }
            }

            return results;
        }

         public Task<int> GetUserWordsCountAsync(long userId, WordStatus? status)
         {
             return userWordRepository.GetUserWordsCountAsync(userId, status);
         }


        public async Task<UserWordDto?> GetUserWordDetailsAsync(long userId, int userWordId)
        {
            var userWord = await userWordRepository.GetSingleAsync(uw => uw.UserId == userId && uw.UserWordId == userWordId);
            if (userWord == null)
            {
                return null;
            }

            var word = await wordRepository.GetSingleAsync(userWord.WordId);
            if (word == null)
            {
                return null;
            }

            return new UserWordDto
            {
                UserWordId = userWord.UserWordId,
                WordId = userWord.WordId,
                Status = userWord.Status,
                AddedAt = DateTime.UnixEpoch.AddSeconds(userWord.CreatedAt),
                WordText = word.WordText,
                WordLanguage = word.WordLanguage,
                UserNativeLanguage = word.ExplanationLanguage,
                Pronunciation = word.Pronunciation,
                Definitions = word.Definitions,
                Examples = word.Examples,
                GeneratedAt = word.CreatedAt > 0 ? DateTime.UnixEpoch.AddSeconds(word.CreatedAt) : null
            };
        }

        public async Task<bool> UpdateWordStatusAsync(long userId, int userWordId, WordStatus newStatus)
        {
            var userWord = await userWordRepository.GetSingleAsync(uw => uw.UserId == userId && uw.UserWordId == userWordId);
            if (userWord == null)
            {
                return false;
            }

            userWord.Status = newStatus;
            return await userWordRepository.UpdateAsync(userWord);
        }

        public async Task<bool> DeleteWordAsync(int userId, int userWordId)
        {
            return await userWordRepository.DeleteAsync(uw => uw.UserWordId == userWordId && uw.UserId == userId);
        }

        // Helper method for mapping
        private UserWordDto MapToUserWordDto(UserWord uw, Word w)
        {
            return new UserWordDto
            {
                UserWordId = uw.UserWordId,
                WordId = uw.WordId,
                Status = uw.Status,
                AddedAt = DateTime.UnixEpoch.AddSeconds(uw.CreatedAt),
                WordText = w.WordText,
                WordLanguage = w.WordLanguage,
                UserNativeLanguage = w.ExplanationLanguage,
                Pronunciation = w.Pronunciation,
                Definitions = w.Definitions,
                Examples = w.Examples,
                GeneratedAt = w.CreatedAt > 0 ? DateTime.UnixEpoch.AddSeconds(w.CreatedAt) : null
            };
        }
    }
}
