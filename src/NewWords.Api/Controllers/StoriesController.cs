using Api.Framework.Models;
using Api.Framework.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Entities;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Controllers
{
    [Authorize]
    public class StoriesController(
        IStoryService storyService,
        ICurrentUser currentUser)
        : BaseController
    {
        /// <summary>
        /// Gets the current user's stories.
        /// </summary>
        /// <param name="pageSize">Number of stories per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <returns>Paginated list of user's stories.</returns>
        [HttpGet]
        public async Task<ApiResult<PageData<Story>>> MyStories(int pageSize = 10, int pageNumber = 1)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var stories = await storyService.GetUserStoriesAsync(userId, pageSize, pageNumber);
            return new SuccessfulResult<PageData<Story>>(stories);
        }

        /// <summary>
        /// Gets stories from other users for discovery (Story Square).
        /// </summary>
        /// <param name="pageSize">Number of stories per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <returns>Paginated list of other users' stories sorted by popularity.</returns>
        [HttpGet]
        public async Task<ApiResult<PageData<Story>>> StorySquare(int pageSize = 10, int pageNumber = 1)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var stories = await storyService.GetStorySquareAsync(userId, pageSize, pageNumber);
            return new SuccessfulResult<PageData<Story>>(stories);
        }

        /// <summary>
        /// Gets the current user's favorite stories.
        /// </summary>
        /// <param name="pageSize">Number of stories per page.</param>
        /// <param name="pageNumber">Page number to retrieve.</param>
        /// <returns>Paginated list of user's favorite stories.</returns>
        [HttpGet]
        public async Task<ApiResult<PageData<Story>>> MyFavorite(int pageSize = 10, int pageNumber = 1)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var stories = await storyService.GetUserFavoriteStoriesAsync(userId, pageSize, pageNumber);
            return new SuccessfulResult<PageData<Story>>(stories);
        }

        /// <summary>
        /// Marks a story as read by the current user.
        /// </summary>
        /// <param name="storyId">The ID of the story to mark as read.</param>
        [HttpPost("{storyId}")]
        public async Task<ApiResult> MarkRead(long storyId)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            await storyService.MarkStoryAsReadAsync(userId, storyId);
            return Success("Story marked as read");
        }

        /// <summary>
        /// Toggles favorite status for a story.
        /// </summary>
        /// <param name="storyId">The ID of the story to favorite/unfavorite.</param>
        [HttpPost("{storyId}")]
        public async Task<ApiResult> Favorite(long storyId)
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var isFavorited = await storyService.ToggleFavoriteAsync(userId, storyId);
            var message = isFavorited ? "Story favorited" : "Story unfavorited";
            return Success(message);
        }

        /// <summary>
        /// Manually generates a story for the current user.
        /// </summary>
        [HttpPost]
        public async Task<ApiResult<Story>> Generate()
        {
            var userId = currentUser.Id;
            if (userId == 0)
            {
                throw new ArgumentException("User not authenticated or ID not found.");
            }

            var story = await storyService.GenerateStoryForUserAsync(userId);
            if (story == null)
            {
                throw new Exception("Unable to generate story. Please ensure you have recent vocabulary words.");
            }

            return new SuccessfulResult<Story>(story, "Story generated successfully");
        }
    }
}