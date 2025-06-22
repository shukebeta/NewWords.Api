using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Models.DTOs.User;
using NewWords.Api.Services;
using Api.Framework.Result;
using Api.Framework.Models;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Controllers
{
    [Authorize]
    public class UserController(IUserService userService) : BaseController
    {
        /// <summary>
        /// Retrieves the current user's profile.
        /// </summary>
        /// <returns>User profile information.</returns>
        [HttpGet]
        public async Task<ApiResult<UserProfileDto>> MyProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out var userId))
            {
                throw new Exception("Invalid user ID.");
            }

            var userProfile = await userService.GetUserProfileAsync(userId);
            if (userProfile is null)
            {
                throw new Exception("User profile not found.");
            }
            return new SuccessfulResult<UserProfileDto>(userProfile, "Profile retrieved successfully.");
        }

        /// <summary>
        /// Updates the current user's profile.
        /// </summary>
        /// <param name="updateDto">The updated profile information.</param>
        /// <returns>Confirmation of update.</returns>
        [HttpPut]
        public async Task<ApiResult> UpdateMyProfile([FromBody] UpdateProfileRequestDto updateDto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out var userId))
            {
                throw new Exception("Invalid user ID.");
            }

            var success = await userService.UpdateUserProfileAsync(userId, updateDto);
            if (!success)
            {
                return Fail("User profile not found or update failed.");
            }

            return Success("Profile updated successfully.");
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
            if (!long.TryParse(userIdString, out var userId))
            {
                throw new Exception("Invalid user ID.");
            }

            var users = await userService.GetPagedUsersAsync(pageSize, pageNumber);
            return new SuccessfulResult<PageData<UserProfileDto>>(users, "Users retrieved successfully.");
        }
    }
}
