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
    public class VocabularyController(IVocabularyService vocabularyService, IQueryHistoryService queryHistoryService, ICurrentUser currentUser)
        : BaseController
    {
        /// <summary>
        /// Retrieves a paginated list of the current user's words.
        /// </summary>
        /// <param name="pageSize">Number of words per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <returns>Paginated list of words.</returns>
        [HttpGet]
        public async Task<ApiResult<PageData<WordExplanation>>> List(int pageSize = 10, int pageNumber = 1)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var words = await vocabularyService.GetUserWordsAsync(userId, pageSize, pageNumber);
            return new SuccessfulResult<PageData<WordExplanation>>(words);
        }

        /// <summary>
        /// Adds a new word to the current user's list.
        /// </summary>
        /// <param name="addWordRequest">The word details to add.</param>
        /// <returns>The added word explanation.</returns>
        [HttpPost]
        public async Task<ApiResult<WordExplanation>> Add(AddWordRequest addWordRequest)
        {
            var userId = currentUser.Id;
            var addedWordExplanation = await vocabularyService.AddUserWordAsync(
                userId,
                addWordRequest.WordText,
                addWordRequest.WordLanguage,
                addWordRequest.ExplanationLanguage
            );

            queryHistoryService.LogQueryAsync(addedWordExplanation.WordCollectionId, currentUser.Id);
            return new SuccessfulResult<WordExplanation>(addedWordExplanation);
        }
    }
}
