using Api.Framework.Models;
using NewWords.Api.Entities;

namespace NewWords.Api.Services.interfaces
{
    public interface IStoryService
    {
        /// <summary>
        /// Gets paginated stories for a specific user.
        /// </summary>
        Task<PageData<Story>> GetUserStoriesAsync(int userId, int pageSize, int pageNumber);

        /// <summary>
        /// Gets paginated stories from other users for discovery, sorted by popularity.
        /// </summary>
        Task<PageData<Story>> GetStorySquareAsync(int userId, int pageSize, int pageNumber);

        /// <summary>
        /// Gets paginated stories that the user has favorited.
        /// </summary>
        Task<PageData<Story>> GetUserFavoriteStoriesAsync(int userId, int pageSize, int pageNumber);

        /// <summary>
        /// Marks a story as read by setting FirstReadAt timestamp.
        /// Only marks if not already read.
        /// </summary>
        Task MarkStoryAsReadAsync(int userId, long storyId);

        /// <summary>
        /// Toggles favorite status for a story and updates FavoriteCount.
        /// Returns true if favorited, false if unfavorited.
        /// </summary>
        Task<bool> ToggleFavoriteAsync(int userId, long storyId);

        /// <summary>
        /// Generates a new story for a user based on their recent vocabulary.
        /// Returns null if user doesn't have enough recent words.
        /// </summary>
        Task<Story?> GenerateStoryForUserAsync(int userId);

        /// <summary>
        /// Generates one or more stories for a user with custom word list or recent vocabulary.
        /// Uses batch generation to create multiple stories if there are many words.
        /// Includes duplicate prevention logic.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="customWords">Optional custom word list. If null, uses recent vocabulary.</param>
        /// <param name="learningLanguage">Optional learning language. If null, uses user's current language.</param>
        /// <returns>List of generated stories</returns>
        Task<List<Story>> GenerateStoryWithWordsAsync(int userId, List<string>? customWords = null, string? learningLanguage = null);

        /// <summary>
        /// Generates multiple stories for a user based on their new vocabulary since last generation.
        /// Each story contains up to MaxWordsPerStory words.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of generated stories</returns>
        Task<List<Story>> GenerateMultipleStoriesForUserAsync(int userId);

        /// <summary>
        /// Batch generates stories for all eligible users (used by cron job).
        /// </summary>
        Task GenerateStoriesForEligibleUsersAsync();
    }
}