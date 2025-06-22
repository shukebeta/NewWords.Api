using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents a word in the global collection, language-agnostic.
    /// </summary>
    [SugarTable("WordCollection")]
    [SugarIndex("UQ_WordCollection_WordText", nameof(WordText), OrderByType.Asc, true)] // WordText should be unique
    public class WordCollection
    {
        /// <summary>
        /// Unique identifier for the word in the collection (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// The actual word text (language-agnostic).
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 255)]
        public string WordText { get; set; } = string.Empty;

        /// <summary>
        /// How many times this word has been queried or added by users.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long QueryCount { get; set; } = 0;

        /// <summary>
        /// Timestamp when the word was first added to the collection (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the word was last updated (Unix timestamp, nullable).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? UpdatedAt { get; set; }

        /// <summary>
        /// Timestamp when the word was soft-deleted (Unix timestamp, nullable).
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? DeletedAt { get; set; }
    }
}
