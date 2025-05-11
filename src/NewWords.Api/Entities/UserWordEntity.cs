using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents the association between a user and a word they have added.
    /// </summary>
    [SugarTable("UserWords")]
    [SugarIndex("UQ_UserWords_UserId_WordExplanationId", nameof(UserId), OrderByType.Asc, nameof(WordExplanationId), OrderByType.Asc, true)] // A user should only have a specific explanation once
    public class UserWordEntity
    {
        /// <summary>
        /// Unique identifier for the user-word association (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int UserWordId { get; set; }

        /// <summary>
        /// Identifier of the user. Foreign key to Users table.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long UserId { get; set; } // Changed to long to match UserProfileDto.UserId

        /// <summary>
        /// Identifier of the word explanation. Foreign key to WordExplanations table.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long WordExplanationId { get; set; }

        /// <summary>
        /// Status of the word for the user (e.g., 0: New, 1: Learning, 2: Mastered).
        /// </summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public int Status { get; set; } = 0;

        /// <summary>
        /// Timestamp when the user added this word (Unix timestamp).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }
    }
}