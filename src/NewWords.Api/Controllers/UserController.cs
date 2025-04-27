using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Models.DTOs.User;
using NewWords.Api.Services;
using Api.Framework.Result;

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
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new FailedResult("Invalid user ID."));
            }

            var userProfile = await _userService.GetUserProfileAsync(userId);
            return userProfile == null ? NotFound(new FailedResult("User profile not found.")) : Ok(new SuccessfulResult<UserProfileDto>(userProfile, "Profile retrieved successfully."));
        }

        /// <summary>
        /// Updates the current user's profile.
        /// </summary>
        /// <param name="updateDto">The updated profile information.</param>
        /// <returns>Confirmation of update.</returns>
        [HttpPut("me")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto updateDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new FailedResult("Invalid user ID."));
            }

            var success = await _userService.UpdateUserProfileAsync(userId, updateDto);
            if (!success)
            {
                return NotFound(new FailedResult("User profile not found or update failed."));
            }

            return Ok(new SuccessfulResult<string>("Profile updated successfully."));
        }
    }
}