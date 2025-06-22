using Api.Framework;
using Api.Framework.Models;
using NewWords.Api.Entities;
using NewWords.Api.Repositories;
using NewWords.Api.Services.interfaces;
using SqlSugar;
using LLM.Services;
using LLM;

namespace NewWords.Api.Services
{
    public class StoryService(
        ISqlSugarClient db,
        ILanguageService languageService,
        IConfigurationService configurationService,
        ILogger<StoryService> logger,
        IRepositoryBase<Story> storyRepository,
        IRepositoryBase<StoryWord> storyWordRepository,
        IRepositoryBase<UserFavoriteStory> userFavoriteStoryRepository)
        : IStoryService
    {
        public async Task<PageData<Story>> GetUserStoriesAsync(int userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedStories = await db.Queryable<Story>()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAt, OrderByType.Desc)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<Story>
            {
                DataList = pagedStories,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PageData<Story>> GetStorySquareAsync(int userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedStories = await db.Queryable<Story>()
                .Where(s => s.UserId != userId)
                .OrderBy(s => s.FavoriteCount, OrderByType.Desc)
                .OrderBy(s => s.CreatedAt, OrderByType.Desc)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<Story>
            {
                DataList = pagedStories,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PageData<Story>> GetUserFavoriteStoriesAsync(int userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedStories = await db.Queryable<Story>()
                .InnerJoin<UserFavoriteStory>((s, ufs) => s.Id == ufs.StoryId)
                .Where((s, ufs) => ufs.UserId == userId)
                .OrderBy((s, ufs) => ufs.CreatedAt, OrderByType.Desc)
                .Select((s, ufs) => s)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<Story>
            {
                DataList = pagedStories,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task MarkStoryAsReadAsync(int userId, long storyId)
        {
            var story = await storyRepository.GetFirstOrDefaultAsync(s => s.Id == storyId && s.UserId == userId);
            if (story == null)
            {
                logger.LogWarning($"Story not found or not owned by user - StoryId: {storyId}, UserId: {userId}");
                return;
            }

            if (story.FirstReadAt == null)
            {
                story.FirstReadAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await storyRepository.UpdateAsync(story);
                logger.LogInformation($"Story marked as read - StoryId: {storyId}, UserId: {userId}");
            }
        }

        public async Task<bool> ToggleFavoriteAsync(int userId, long storyId)
        {
            try
            {
                await db.AsTenant().BeginTranAsync();
                
                var story = await storyRepository.GetFirstOrDefaultAsync(s => s.Id == storyId);
                if (story == null)
                {
                    logger.LogWarning($"Story not found for favorite toggle - StoryId: {storyId}");
                    await db.AsTenant().RollbackTranAsync();
                    return false;
                }

                // Check if user has already favorited this story
                var existingFavorite = await userFavoriteStoryRepository.GetFirstOrDefaultAsync(
                    ufs => ufs.UserId == userId && ufs.StoryId == storyId);

                bool isFavorited;
                if (existingFavorite != null)
                {
                    // Remove favorite
                    await userFavoriteStoryRepository.DeleteAsync(existingFavorite);
                    story.FavoriteCount = Math.Max(0, story.FavoriteCount - 1);
                    isFavorited = false;
                    logger.LogInformation($"Story unfavorited - StoryId: {storyId}, UserId: {userId}");
                }
                else
                {
                    // Add favorite
                    var newFavorite = new UserFavoriteStory
                    {
                        UserId = userId,
                        StoryId = storyId,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    await userFavoriteStoryRepository.InsertAsync(newFavorite);
                    story.FavoriteCount++;
                    isFavorited = true;
                    logger.LogInformation($"Story favorited - StoryId: {storyId}, UserId: {userId}");
                }

                await storyRepository.UpdateAsync(story);
                await db.AsTenant().CommitTranAsync();
                
                return isFavorited;
            }
            catch (Exception ex)
            {
                await db.AsTenant().RollbackTranAsync();
                logger.LogError(ex, $"Error toggling favorite for StoryId: {storyId}, UserId: {userId}");
                throw;
            }
        }

        public async Task<Story?> GenerateStoryForUserAsync(int userId)
        {
            try
            {
                // Get user's recent vocabulary words
                var recentWords = await GetUserRecentWordsAsync(userId);
                if (recentWords.Count < 3)
                {
                    logger.LogInformation($"User {userId} doesn't have enough recent words for story generation. Found: {recentWords.Count}");
                    return null;
                }

                // Get user's learning language
                var user = await db.Queryable<User>().FirstAsync(u => u.Id == userId);
                if (user == null)
                {
                    logger.LogWarning($"User not found - UserId: {userId}");
                    return null;
                }

                // Generate story using AI
                var (storyContent, modelName) = await GenerateStoryContentAsync(recentWords, user.CurrentLearningLanguage);
                if (string.IsNullOrWhiteSpace(storyContent))
                {
                    logger.LogError($"Failed to generate story content for user {userId}");
                    return null;
                }

                // Create story entity
                var story = new Story
                {
                    UserId = userId,
                    Content = storyContent,
                    StoryWords = string.Join(", ", recentWords.Select(w => w.WordText)),
                    LearningLanguage = user.CurrentLearningLanguage,
                    ProviderModelName = modelName,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                // Save story
                var storyId = await storyRepository.InsertReturnIdentityAsync(story);
                story.Id = storyId;

                // Save story-word relationships
                await SaveStoryWordsAsync(storyId, recentWords);

                logger.LogInformation($"Story generated successfully for user {userId}, StoryId: {storyId}");
                return story;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error generating story for user {userId}");
                return null;
            }
        }

        public async Task GenerateStoriesForEligibleUsersAsync()
        {
            try
            {
                // Get users who have recent words and don't have a story from today
                var today = DateTimeOffset.UtcNow.Date;
                var todayUnix = ((DateTimeOffset)today).ToUnixTimeSeconds();
                var sevenDaysAgo = today.AddDays(-7);
                var sevenDaysAgoUnix = ((DateTimeOffset)sevenDaysAgo).ToUnixTimeSeconds();

                var eligibleUsers = await db.Queryable<User>()
                    .Where(u => SqlFunc.Subqueryable<UserWord>()
                        .InnerJoin<WordExplanation>((uw, we) => uw.WordExplanationId == we.Id)
                        .Where((uw, we) => uw.UserId == u.Id && 
                                         uw.CreatedAt > sevenDaysAgoUnix &&
                                         we.LearningLanguage == u.CurrentLearningLanguage)
                        .Count() >= 5)
                    .Where(u => !SqlFunc.Subqueryable<Story>()
                        .Where(s => s.UserId == u.Id && s.CreatedAt > todayUnix)
                        .Any())
                    .ToListAsync();

                logger.LogInformation($"Found {eligibleUsers.Count} eligible users for story generation");

                foreach (var user in eligibleUsers)
                {
                    try
                    {
                        await GenerateStoryForUserAsync(user.Id);
                        // Add delay to respect API rate limits
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to generate story for user {user.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in batch story generation");
            }
        }

        private async Task<List<WordCollection>> GetUserRecentWordsAsync(int userId)
        {
            var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
            
            return await db.Queryable<WordCollection>()
                .InnerJoin<WordExplanation>((wc, we) => wc.Id == we.WordCollectionId)
                .InnerJoin<UserWord>((wc, we, uw) => we.Id == uw.WordExplanationId)
                .InnerJoin<User>((wc, we, uw, u) => uw.UserId == u.Id)
                .Where((wc, we, uw, u) => uw.UserId == userId && 
                                        uw.CreatedAt > sevenDaysAgo &&
                                        we.LearningLanguage == u.CurrentLearningLanguage)
                .Select((wc, we, uw, u) => wc)
                .Distinct()
                .Take(8)
                .ToListAsync();
        }

        private async Task<(string content, string? modelName)> GenerateStoryContentAsync(List<WordCollection> words, string learningLanguage)
        {
            try
            {
                var wordList = string.Join(", ", words.Select(w => w.WordText));
                var languageName = configurationService.GetLanguageName(learningLanguage) ?? "English";
                
                // Use the dedicated story generation method
                var result = await languageService.GetStoryWithFallbackAsync(wordList, languageName);
                
                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Content))
                {
                    return (result.Content, result.ModelName);
                }
                
                logger.LogError($"AI story generation failed: {result.ErrorMessage}");
                return (string.Empty, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AI story generation");
                return (string.Empty, null);
            }
        }

        private async Task SaveStoryWordsAsync(long storyId, List<WordCollection> words)
        {
            var storyWords = words.Select(w => new StoryWord
            {
                StoryId = storyId,
                WordCollectionId = w.Id
            }).ToList();

            foreach (var storyWord in storyWords)
            {
                await storyWordRepository.InsertAsync(storyWord);
            }
        }
    }
}