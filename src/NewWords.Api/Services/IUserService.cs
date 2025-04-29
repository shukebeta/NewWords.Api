using NewWords.Api.Models.DTOs.User;
using System.Threading.Tasks;

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
        Task<UserProfileDto?> GetUserProfileAsync(int userId);

        /// <summary>
        /// Updates the profile information for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user whose profile to update.</param>
        /// <param name="updateDto">The data to update.</param>
        /// <returns>True if the update was successful, false otherwise (e.g., user not found).</returns>
        Task<bool> UpdateUserProfileAsync(int userId, UpdateProfileRequestDto updateDto);
    }
}