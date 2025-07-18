using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Junction table tracking when users read specific stories.
    /// </summary>
    [SugarTable("UserStoryReads")]
    public class UserStoryRead
    {
        /// <summary>
        /// Unique identifier for the user-story read relationship (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// The user who read the story.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// The story that was read.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long StoryId { get; set; }

        /// <summary>
        /// Unix timestamp when the user first read the story.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long FirstReadAt { get; set; }
    }
}