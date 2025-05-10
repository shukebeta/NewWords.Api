using Api.Framework.Models;
using NewWords.Api.Models.DTOs.User;

namespace NewWords.Api.Services
{
    /// <summary>
    /// Interface for user profile management services.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Gets the profile information for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user whose profile to retrieve.</param>
        /// <returns>A UserProfileDto if the user is found, otherwise null.</returns>
        Task<UserProfileDto?> GetUserProfileAsync(long userId);

        /// <summary>
        /// Updates the profile information for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user whose profile to update.</param>
        /// <param name="updateDto">The data to update.</param>
        /// <returns>True if the update was successful, false otherwise (e.g., user not found).</returns>
        Task<bool> UpdateUserProfileAsync(long userId, UpdateProfileRequestDto updateDto);

        /// <summary>
        /// Retrieves a paginated list of user profiles.
        /// </summary>
        /// <param name="pageSize">Number of users per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <param name="isAsc">Whether to sort in ascending order.</param>
        /// <returns>Paginated list of user profiles.</returns>
        Task<PageData<UserProfileDto>> GetPagedUsersAsync(int pageSize, int pageNumber, bool isAsc = false);
    }
}
