using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Junction table tracking which users have favorited which stories.
    /// </summary>
    [SugarTable("UserFavoriteStories")]
    public class UserFavoriteStory
    {
        /// <summary>
        /// Unique identifier for the user-story favorite relationship (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// The user who favorited the story.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// The story that was favorited.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long StoryId { get; set; }

        /// <summary>
        /// Unix timestamp when the user favorited the story.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }
    }
}