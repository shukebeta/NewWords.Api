using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Models.DTOs.User;
using NewWords.Api.Services;
using Api.Framework.Result;
using Api.Framework.Models;

namespace NewWords.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Retrieves the current user's profile.
        /// </summary>
        /// <returns>User profile information.</returns>
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SuccessfulResult<UserProfileDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FailedResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(FailedResult), StatusCodes.Status404NotFound)]
        public async Task<ApiResult<UserProfileDto>> GetMyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return new FailedResult<UserProfileDto>(default, "Invalid user ID.");
            }

            var userProfile = await _userService.GetUserProfileAsync(userId);
            return userProfile == null ? new FailedResult<UserProfileDto>(default, "User profile not found.") : new SuccessfulResult<UserProfileDto>(userProfile, "Profile retrieved successfully.");
        }

        /// <summary>
        /// Updates the current user's profile.
        /// </summary>
        /// <param name="updateDto">The updated profile information.</param>
        /// <returns>Confirmation of update.</returns>
        [HttpPut("me")]
        public async Task<ApiResult<string>> UpdateMyProfile([FromBody] UpdateProfileRequestDto updateDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return new FailedResult<string>(default, "Invalid user ID.");
            }

            var success = await _userService.UpdateUserProfileAsync(userId, updateDto);
            if (!success)
            {
                return new FailedResult<string>(default, "User profile not found or update failed.");
            }

            return new SuccessfulResult<string>("Profile updated successfully.");
        }

        /// <summary>
        /// Retrieves a paginated list of users.
        /// </summary>
        /// <param name="pageSize">Number of users per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <returns>Paginated list of user profiles.</returns>
        [HttpGet("list/{pageSize:int}/{pageNumber:int}")]
        [EnforcePageSizeLimit(50)]
        public async Task<ApiResult<PageData<UserProfileDto>>> GetUsers(int pageSize, int pageNumber)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return new FailedResult<PageData<UserProfileDto>>(default, "Invalid user ID.");
            }

            var users = await _userService.GetPagedUsersAsync(pageSize, pageNumber);
            return new SuccessfulResult<PageData<UserProfileDto>>(users, "Users retrieved successfully.");
        }
    }
}