namespace NewWords.Api.Models.DTOs.Stories
{
    /// <summary>
    /// Data Transfer Object for Story with user-specific information.
    /// </summary>
    public class StoryDto
    {
        /// <summary>
        /// Unique identifier for the story.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The user who owns this story.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// The story content with markdown formatting (bold words).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated list of vocabulary words used in the story.
        /// </summary>
        public string StoryWords { get; set; } = string.Empty;

        /// <summary>
        /// The learning language of the story.
        /// </summary>
        public string LearningLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Unix timestamp when the user first read this story (NULL = unread).
        /// </summary>
        public long? FirstReadAt { get; set; }

        /// <summary>
        /// Number of times this story has been favorited by users.
        /// </summary>
        public int FavoriteCount { get; set; }

        /// <summary>
        /// Whether the current user has favorited this story.
        /// </summary>
        public bool IsFavorited { get; set; }

        /// <summary>
        /// The AI model that generated this story.
        /// </summary>
        public string? ProviderModelName { get; set; }

        /// <summary>
        /// Unix timestamp when the story was created.
        /// </summary>
        public long CreatedAt { get; set; }
    }
}