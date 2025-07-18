using NewWords.Api.Models.DTOs.User;

namespace NewWords.Api.Services.interfaces
{
    /// <summary>
    /// Interface for account management services.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Updates the language preferences for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user whose languages to update.</param>
        /// <param name="updateDto">The language data to update.</param>
        /// <returns>True if the update was successful, false otherwise (e.g., user not found).</returns>
        Task<bool> UpdateUserLanguagesAsync(long userId, UpdateLanguagesRequestDto updateDto);
    }
}