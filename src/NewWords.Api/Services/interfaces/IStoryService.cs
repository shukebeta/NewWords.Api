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
        /// Batch generates stories for all eligible users (used by cron job).
        /// </summary>
        Task GenerateStoriesForEligibleUsersAsync();
    }
}