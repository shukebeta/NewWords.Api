using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Enums;
using NewWords.Api.Models.DTOs.Vocabulary;
using NewWords.Api.Services;
using Api.Framework.Result;

namespace NewWords.Api.Controllers
{
    [ApiController]
    [Route("api/userwords")]
    [Authorize]
    public class VocabularyController : ControllerBase
    {
        private readonly IVocabularyService _vocabService;

        public VocabularyController(IVocabularyService vocabService)
        {
            _vocabService = vocabService;
        }

        /// <summary>
        /// Adds a new word for the user.
        /// </summary>
        /// <param name="addDto">The word details to add.</param>
        /// <returns>The added word details.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(UserWordDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddUserWord([FromBody] AddWordRequestDto addDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized(new FailedResult("Invalid user ID."));

            var resultDto = await _vocabService.AddWordAsync(userId, addDto);
            if (resultDto == null)
            {
                return BadRequest(new FailedResult("Failed to add word."));
            }
            return Ok(new SuccessfulResult<UserWordDto>(resultDto, "Word added successfully."));
        }

        /// <summary>
        /// Retrieves a paginated list of user words, optionally filtered by status.
        /// </summary>
        /// <param name="status">Optional status filter for words.</param>
        /// <param name="page">Page number for pagination.</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>Paginated list of user words.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUserWords([FromQuery] WordStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized(new FailedResult("Invalid user ID."));

            pageSize = Math.Clamp(pageSize, 1, 50);

            var words = await _vocabService.GetUserWordsAsync(userId, status, page, pageSize);
            var totalCount = await _vocabService.GetUserWordsCountAsync(userId, status);

            return Ok(new SuccessfulResult<object>(new { TotalCount = totalCount, Page = page, PageSize = pageSize, Items = words }, "User words retrieved successfully."));
        }

        /// <summary>
        /// Retrieves details of a specific user word.
        /// </summary>
        /// <param name="userWordId">The ID of the word to retrieve.</param>
        /// <returns>Details of the specified word.</returns>
        [HttpGet("{userWordId:int}")]
        [ProducesResponseType(typeof(UserWordDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserWordDetails(int userWordId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized(new FailedResult("Invalid user ID."));

            var wordDetails = await _vocabService.GetUserWordDetailsAsync(userId, userWordId);
            return wordDetails == null ? NotFound(new FailedResult("Word not found.")) : Ok(new SuccessfulResult<UserWordDto>(wordDetails, "Word details retrieved successfully."));
        }

        /// <summary>
        /// Updates the status of a specific user word.
        /// </summary>
        /// <param name="userWordId">The ID of the word to update.</param>
        /// <param name="updateDto">The new status for the word.</param>
        /// <returns>Confirmation of status update.</returns>
        [HttpPut("{userWordId:int}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUserWordStatus(int userWordId, [FromBody] UpdateWordStatusRequestDto updateDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized(new FailedResult("Invalid user ID."));

            var success = await _vocabService.UpdateWordStatusAsync(userId, userWordId, updateDto.NewStatus);
            if (!success)
            {
                return NotFound(new FailedResult("Word entry not found or update failed."));
            }
            return Ok(new SuccessfulResult<string>("Word status updated successfully."));
        }

        /// <summary>
        /// Deletes a specific user word.
        /// </summary>
        /// <param name="userWordId">The ID of the word to delete.</param>
        /// <returns>Confirmation of deletion.</returns>
        [HttpDelete("{userWordId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUserWord(int userWordId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId)) return Unauthorized(new FailedResult("Invalid user ID."));

            var success = await _vocabService.DeleteWordAsync(userId, userWordId);
            if (!success)
            {
                return NotFound(new FailedResult("Word entry not found or delete failed."));
            }
            return Ok(new SuccessfulResult<string>("Word deleted successfully."));
        }
    }
}