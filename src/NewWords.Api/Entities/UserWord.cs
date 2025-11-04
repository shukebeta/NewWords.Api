using SqlSugar;
using NewWords.Api.Enums; // Import the enum namespace

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents the link between a user and a specific word explanation,
    /// including the user's learning status for that word.
    /// </summary>
    [SugarTable("UserWords")]
    // Define the unique constraint for the combination of UserId and WordExplanationId
    [SugarIndex("UQ_UserWords_UserId_WordExplanationId", nameof(UserId), OrderByType.Desc, nameof(WordExplanationId), OrderByType.Asc, true)]
    public class UserWord
    {
        /// <summary>
        /// Unique identifier for this user-word link (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// Foreign key referencing the User. Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int UserId { get; set; }

        /// <summary>
        /// Foreign key referencing the word collection. Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long WordCollectionId { get; set; }

        /// <summary>
        /// Foreign key referencing the specific Word explanation. Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long WordExplanationId { get; set; }

        /// <summary>
        /// The user's current learning status for this word. Required. Defaults to New.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public FamiliarityLevel Status { get; set; } = FamiliarityLevel.New;

        /// <summary>
        /// Timestamp when the user added this word to their list (Required, Unix timestamp as long).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; } = 0; // Default to 0 (Unix epoch start), renamed to CreatedAt for consistency
        /// <summary>
        /// Timestamp when the user last interacted with this word (Required, Unix timestamp as long).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long UpdatedAt { get; set; } = 0;


        // --- Navigation Properties (Optional but recommended for ORM convenience) ---

        /// <summary>
        /// Navigation property back to the User. Ignored by SqlSugar for mapping.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public User? User { get; set; }

        /// <summary>
        /// Navigation property back to the Word explanation. Ignored by SqlSugar for mapping.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public WordExplanation? WordExplanation { get; set; }

        /// <summary>
        /// Navigation property back to the Word collection. Ignored by SqlSugar for mapping.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public WordCollection? WordCollection { get; set; }
    }
}
