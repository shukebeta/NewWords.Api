using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Enums;
using NewWords.Api.Models.DTOs.Vocabulary;
using NewWords.Api.Services;
using Api.Framework.Result;

namespace NewWords.Api.Controllers
{
    [Authorize]
    public class VocabularyController(IVocabularyService vocabService) : BaseController
    {
        /// <summary>
        /// Adds a new word for the user.
        /// </summary>
        /// <param name="addDto">The word details to add.</param>
        /// <returns>The added word details.</returns>
        [HttpPost]
        public async Task<ApiResult<UserWordDto>> AddWord([FromBody] AddWordRequestDto addDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return new FailedResult<UserWordDto>(default, "Invalid user ID.");

            var resultDto = await vocabService.AddWordAsync(userId, addDto);
            if (resultDto == null)
            {
                throw new Exception("Failed to add word.");
            }
            return new SuccessfulResult<UserWordDto>(resultDto, "Word added successfully.");
        }

        /// <summary>
        /// Retrieves a paginated list of user words, optionally filtered by status.
        /// </summary>
        /// <param name="status">Optional status filter for words.</param>
        /// <param name="page">Page number for pagination.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>Paginated list of user words.</returns>
        [HttpGet]
        public async Task<ApiResult<object>> GetWords(WordStatus? status, int page = 1, int pageSize = 10)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId)) return new FailedResult<object>(default, "Invalid user ID.");

            pageSize = Math.Clamp(pageSize, 1, 50);

            var words = await vocabService.GetUserWordsAsync(userId, status, page, pageSize);
            var totalCount = await vocabService.GetUserWordsCountAsync(userId, status);

            return new SuccessfulResult<object>(new { TotalCount = totalCount, Page = page, PageSize = pageSize, Items = words }, "User words retrieved successfully.");
        }

        /// <summary>
        /// Retrieves details of a specific user word.
        /// </summary>
        /// <param name="userWordId">The ID of the word to retrieve.</param>
        /// <returns>Details of the specified word.</returns>
        [HttpGet("{userWordId:int}")]
        public async Task<ApiResult<UserWordDto>> GetUserWordDetails(int userWordId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return new FailedResult<UserWordDto>(default, "Invalid user ID.");

            var wordDetails = await vocabService.GetUserWordDetailsAsync(userId, userWordId);
            return wordDetails == null ? new FailedResult<UserWordDto>(default, "Word not found.") : new SuccessfulResult<UserWordDto>(wordDetails, "Word details retrieved successfully.");
        }

        /// <summary>
        /// Updates the status of a specific user word.
        /// </summary>
        /// <param name="userWordId">The ID of the word to update.</param>
        /// <param name="updateDto">The new status for the word.</param>
        /// <returns>Confirmation of status update.</returns>
        [HttpPut("{userWordId:int}/status")]
        public async Task<ApiResult<string>> UpdateUserWordStatus(int userWordId, [FromBody] UpdateWordStatusRequestDto updateDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return new FailedResult<string>(default, "Invalid user ID.");

            var success = await vocabService.UpdateWordStatusAsync(userId, userWordId, updateDto.NewStatus);
            if (!success)
            {
                return new FailedResult<string>(default, "Word entry not found or update failed.");
            }
            return new SuccessfulResult<string>("Word status updated successfully.");
        }

        /// <summary>
        /// Deletes a specific user word.
        /// </summary>
        /// <param name="userWordId">The ID of the word to delete.</param>
        /// <returns>Confirmation of deletion.</returns>
        [HttpDelete("{userWordId:int}")]
        public async Task<ApiResult<string>> DeleteUserWord(int userWordId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return new FailedResult<string>(default, "Invalid user ID.");

            var success = await vocabService.DeleteWordAsync(userId, userWordId);
            if (!success)
            {
                return new FailedResult<string>(default, "Word entry not found or delete failed.");
            }
            return new SuccessfulResult<string>("Word deleted successfully.");
        }
    }
}
