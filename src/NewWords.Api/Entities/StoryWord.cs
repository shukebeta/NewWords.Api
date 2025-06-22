using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Junction table linking stories to the vocabulary words they contain.
    /// Used for future similarity calculations and analytics.
    /// </summary>
    [SugarTable("StoryWords")]
    public class StoryWord
    {
        /// <summary>
        /// Unique identifier for the story-word relationship (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// Reference to the story.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long StoryId { get; set; }

        /// <summary>
        /// Reference to the word collection (vocabulary concept).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long WordCollectionId { get; set; }
    }
}