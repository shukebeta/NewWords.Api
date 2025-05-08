using SqlSugar;
// Removed Api.Framework import as EntityBase is not used based on the plan

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents a word collected from various sources, typically for pre-processing.
    /// </summary>
    [SugarTable("WordCollection")]
    public class WordCollection
    {
        /// <summary>
        /// Unique identifier for the collected word record (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// The actual word text. Required.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 255)]
        public string WordText { get; set; } = string.Empty;

        /// <summary>
        /// The language code of the word (e.g., "English"). Required.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Count of how many times this word has been queried or processed. Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long QueryCount { get; set; }

        /// <summary>
        /// Timestamp when the record was created (Unix timestamp). Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the record was last updated (Unix timestamp). Nullable.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? UpdatedAt { get; set; }

        /// <summary>
        /// Timestamp when the record was marked as deleted (Unix timestamp). Nullable.
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? DeletedAt { get; set; }
    }
}