using Api.Framework.Models;
using Api.Framework.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Entities;
using NewWords.Api.Services;
using NewWords.Api.Models.DTOs.Vocabulary;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Controllers
{
    [Authorize]
    public class VocabularyController(IVocabularyService vocabularyService, ICurrentUser currentUser)
        : BaseController
    {
        /// <summary>
        /// Retrieves a paginated list of the current user's words.
        /// </summary>
        /// <param name="pageSize">Number of words per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <returns>Paginated list of words.</returns>
        [HttpGet]
        [EnforcePageSizeLimit(50)]
        public async Task<ApiResult<PageData<Word>>> List(int pageSize = 10, int pageNumber = 1)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var words = await vocabularyService.GetUserWordsAsync(userId, pageSize, pageNumber);
            return new SuccessfulResult<PageData<Word>>(words);
        }

        /// <summary>
        /// Adds a new word to the current user's list.
        /// </summary>
        /// <param name="addWordRequestDto">The word details to add.</param>
        /// <returns>The added word.</returns>
        [HttpPost]
        public async Task<ApiResult<Word>> Add(AddWordRequestDto addWordRequestDto)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var wordToAdd = new Word
            {
                WordText = addWordRequestDto.WordText,
                WordLanguage = addWordRequestDto.WordLanguage,
                ExplanationLanguage = addWordRequestDto.ExplanationLanguage,
                MarkdownExplanation = addWordRequestDto.MarkdownExplanation,
                Pronunciation = addWordRequestDto.Pronunciation,
                Definitions = addWordRequestDto.Definitions,
                Examples = addWordRequestDto.Examples,
                ProviderModelName = addWordRequestDto.ProviderModelName,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var addedWord = await vocabularyService.AddUserWordAsync(userId, wordToAdd);
            return new SuccessfulResult<Word>(addedWord);
        }
    }
}
