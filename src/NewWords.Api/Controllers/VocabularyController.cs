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
    public class VocabularyController(
        IVocabularyService vocabularyService,
        IQueryHistoryService queryHistoryService,
        ICurrentUser currentUser)
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
                addWordRequest.LearningLanguage,
                addWordRequest.ExplanationLanguage
            );

            queryHistoryService.LogQueryAsync(addedWordExplanation.WordCollectionId, currentUser.Id);
            return new SuccessfulResult<WordExplanation>(addedWordExplanation);
        }

        /// <summary>
        /// Deletes a word from the current user's list.
        /// </summary>
        /// <param name="wordExplanationId">The ID of the word explanation to delete.</param>
        [HttpDelete("{wordExplanationId}")]
        public async Task<ApiResult> Delete(long wordExplanationId)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            await vocabularyService.DelUserWordAsync(userId, wordExplanationId);
            return Success();
        }

        /// <summary>
        /// Refreshes the explanation for a word using the current first agent if different from the original provider.
        /// </summary>
        /// <param name="wordExplanationId">The ID of the word explanation to refresh.</param>
        /// <returns>The refreshed word explanation.</returns>
        [HttpPut("{wordExplanationId}")]
        public async Task<ApiResult<WordExplanation>> RefreshExplanation(long wordExplanationId)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var refreshedExplanation = await vocabularyService.RefreshUserWordExplanationAsync(wordExplanationId);
            return new SuccessfulResult<WordExplanation>(refreshedExplanation);
        }

        /// <summary>
        /// Retrieves vocabulary memories from various days ago (3 days, 1 week, 2 weeks, etc.) for spaced repetition.
        /// </summary>
        /// <param name="localTimezone">The user's local timezone (e.g., "America/New_York").</param>
        /// <returns>List of word explanations from memory dates.</returns>
        [HttpGet]
        public async Task<ApiResult<List<WordExplanation>>> Memories(string localTimezone)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var memories = await vocabularyService.MemoriesAsync(userId, localTimezone);
            return new SuccessfulResult<List<WordExplanation>>(memories.ToList());
        }

        /// <summary>
        /// Retrieves all vocabulary words learned on a specific date.
        /// </summary>
        /// <param name="localTimezone">The user's local timezone (e.g., "America/New_York").</param>
        /// <param name="yyyyMMdd">The date in YYYYMMDD format (e.g., "20240115").</param>
        /// <returns>List of word explanations from the specified date.</returns>
        [HttpGet]
        public async Task<ApiResult<List<WordExplanation>>> MemoriesOn(string localTimezone, string yyyyMMdd)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var memories = await vocabularyService.MemoriesOnAsync(userId, localTimezone, yyyyMMdd);
            return new SuccessfulResult<List<WordExplanation>>(memories.ToList());
        }
    }
}
