using NewWords.Api.Models.DTOs.Vocabulary;
using NewWords.Api.Enums;

namespace NewWords.Api.Services
{
    /// <summary>
    /// Interface for managing user vocabulary entries.
    /// </summary>
    public interface IVocabularyService
    {
        /// <summary>
        /// Adds a new word to a user's vocabulary list.
        /// Handles finding or creating the canonical Word entry and linking it via UserWord.
        /// May trigger background LLM generation if the Word entry is new.
        /// </summary>
        /// <param name="userId">The ID of the user adding the word.</param>
        /// <param name="addWordDto">The word text to add.</param>
        /// <returns>The newly created or linked UserWordDto, or null if the user is not found or an error occurs.</returns>
        Task<UserWordDto?> AddWordAsync(long userId, AddWordRequestDto addWordDto);

        /// <summary>
        /// Gets a paginated list of vocabulary words for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="status">Optional filter by word status.</param>
        /// <param name="page">The page number (1-based).</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>A list of UserWordDto objects.</returns>
        Task<List<UserWordDto>> GetUserWordsAsync(long userId, WordStatus? status, int page, int pageSize);

        /// <summary>
        /// Gets the total count of vocabulary words for a specific user, optionally filtered by status.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="status">Optional filter by word status.</param>
        /// <returns>The total count of matching words.</returns>
        Task<int> GetUserWordsCountAsync(long userId, WordStatus? status);


        /// <summary>
        /// Gets the details of a specific vocabulary entry for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="userWordId">The ID of the UserWord entry.</param>
        /// <returns>The UserWordDto if found and belongs to the user, otherwise null.</returns>
        Task<UserWordDto?> GetUserWordDetailsAsync(long userId, int userWordId);

        /// <summary>
        /// Updates the learning status of a specific vocabulary entry for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="userWordId">The ID of the UserWord entry to update.</param>
        /// <param name="newStatus">The new status to set.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        Task<bool> UpdateWordStatusAsync(long userId, int userWordId, WordStatus newStatus);

        /// <summary>
        /// Deletes a vocabulary entry for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="userWordId">The ID of the UserWord entry to delete.</param>
        /// <returns>True if the deletion was successful, false otherwise.</returns>
        Task<bool> DeleteWordAsync(int userId, int userWordId);
    }
}
