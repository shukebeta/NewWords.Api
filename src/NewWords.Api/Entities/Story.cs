using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents an AI-generated story for vocabulary learning.
    /// </summary>
    [SugarTable("Stories")]
    public class Story
    {
        /// <summary>
        /// Unique identifier for the story (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// The user who owns this story.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// The story content with markdown formatting (bold words).
        /// </summary>
        [SugarColumn(IsNullable = false, ColumnDataType = "TEXT")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated list of vocabulary words used in the story for fast display.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 1024)]
        public string StoryWords { get; set; } = string.Empty;

        /// <summary>
        /// The learning language of the story (e.g., "en", "zh", "es").
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string LearningLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Unix timestamp when the user first read this story (NULL = unread).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? FirstReadAt { get; set; }

        /// <summary>
        /// Number of times this story has been favorited by users.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int FavoriteCount { get; set; } = 0;

        /// <summary>
        /// The AI model that generated this story.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? ProviderModelName { get; set; }

        /// <summary>
        /// Unix timestamp when the story was created.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }
    }
}