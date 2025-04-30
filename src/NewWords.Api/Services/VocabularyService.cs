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
    public class VocabularyService : IVocabularyService
    {
        private readonly Repositories.IUserRepository _userRepository;
        private readonly Repositories.IWordRepository _wordRepository;
        private readonly Repositories.IUserWordRepository _userWordRepository;
        // TODO: Inject a background job client service (e.g., IBackgroundJobClient from Hangfire) later

        public VocabularyService(
            Repositories.IUserRepository userRepository,
            Repositories.IWordRepository wordRepository,
            Repositories.IUserWordRepository userWordRepository)
        {
            _userRepository = userRepository;
            _wordRepository = wordRepository;
            _userWordRepository = userWordRepository;
        }

        public async Task<UserWordDto?> AddWordAsync(int userId, AddWordRequestDto addWordDto)
        {
            // 1. Get user details (needed for languages)
            var user = await _userRepository.GetByIdAsync(userId);
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
            var wordEntry = await _wordRepository.GetByTextAndLanguageAsync(wordText, wordLanguage, userNativeLanguage);

            bool needsGeneration = false;
            if (wordEntry == null)
            {
                // Create new Word entry
                wordEntry = new Word
                {
                    WordText = wordText,
                    WordLanguage = wordLanguage,
                    UserNativeLanguage = userNativeLanguage,
                    // LLM fields are initially null
                    CreatedAt = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
                };
                await _wordRepository.InsertAsync(wordEntry);
                needsGeneration = true;
            }
            else if (string.IsNullOrEmpty(wordEntry.Definitions) && string.IsNullOrEmpty(wordEntry.Examples) && string.IsNullOrEmpty(wordEntry.Pronunciation))
            {
                // Word exists but LLM data hasn't been generated yet (or failed previously)
                needsGeneration = true; // Potentially re-trigger generation
            }


            // 4. Find or Create the UserWord link
            var userWordEntry = await _userWordRepository.GetByUserAndWordIdAsync(userId, wordEntry.WordId);

            if (userWordEntry == null)
            {
                userWordEntry = new UserWord
                {
                    UserId = userId,
                    WordId = wordEntry.WordId,
                    Status = WordStatus.New,
                    CreatedAt = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
                };
                await _userWordRepository.InsertAsync(userWordEntry);
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

        public async Task<List<UserWordDto>> GetUserWordsAsync(int userId, WordStatus? status, int page, int pageSize)
        {
            var userWords = await _userWordRepository.GetUserWordsAsync(userId, status, page, pageSize);

            var wordIds = userWords.Select(uw => uw.WordId).ToList();
            var words = await _wordRepository.GetListByIdsAsync(wordIds.ToArray());

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
                        UserNativeLanguage = word.UserNativeLanguage,
                        Pronunciation = word.Pronunciation,
                        Definitions = word.Definitions,
                        Examples = word.Examples,
                        GeneratedAt = word.CreatedAt > 0 ? DateTime.UnixEpoch.AddSeconds(word.CreatedAt) : null
                    });
                }
            }

            return results;
        }

         public Task<int> GetUserWordsCountAsync(int userId, WordStatus? status)
         {
             return _userWordRepository.GetUserWordsCountAsync(userId, status);
         }


        public async Task<UserWordDto?> GetUserWordDetailsAsync(int userId, int userWordId)
        {
            var userWord = await _userWordRepository.GetSingleAsync(uw => uw.UserId == userId && uw.UserWordId == userWordId);
            if (userWord == null)
            {
                return null;
            }

            var word = await _wordRepository.GetSingleAsync(userWord.WordId);
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
                UserNativeLanguage = word.UserNativeLanguage,
                Pronunciation = word.Pronunciation,
                Definitions = word.Definitions,
                Examples = word.Examples,
                GeneratedAt = word.CreatedAt > 0 ? DateTime.UnixEpoch.AddSeconds(word.CreatedAt) : null
            };
        }

        public async Task<bool> UpdateWordStatusAsync(int userId, int userWordId, WordStatus newStatus)
        {
            var userWord = await _userWordRepository.GetSingleAsync(uw => uw.UserId == userId && uw.UserWordId == userWordId);
            if (userWord == null)
            {
                return false;
            }

            userWord.Status = newStatus;
            return await _userWordRepository.UpdateAsync(userWord);
        }

        public async Task<bool> DeleteWordAsync(int userId, int userWordId)
        {
            return await _userWordRepository.DeleteAsync(uw => uw.UserWordId == userWordId && uw.UserId == userId);
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
                UserNativeLanguage = w.UserNativeLanguage,
                Pronunciation = w.Pronunciation,
                Definitions = w.Definitions,
                Examples = w.Examples,
                GeneratedAt = w.CreatedAt > 0 ? DateTime.UnixEpoch.AddSeconds(w.CreatedAt) : null
            };
        }
    }
}
