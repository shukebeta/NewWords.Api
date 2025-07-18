using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents a specific explanation of a word from WordCollection, tailored for a target language.
    /// </summary>
    [SugarTable("WordExplanations")]
    // Define the unique constraint: one explanation per word (from WordCollection) per explanation language.
    [SugarIndex("UQ_WordExplanations_CollectionId_ExplLang", nameof(WordCollectionId), OrderByType.Asc, nameof(ExplanationLanguage), OrderByType.Asc, true)]
    public class WordExplanation
    {
        /// <summary>
        /// Unique identifier for this specific word explanation (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// Foreign key referencing the Id in the WordCollection table. Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long WordCollectionId { get; set; }

        /// <summary>
        /// The actual word text (e.g., "ubiquitous"). Denormalized from WordCollection. Required.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 255)]
        public string WordText { get; set; } = string.Empty;

        /// <summary>
        /// The user's learning language code (e.g., "en", "zh"). Required.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string LearningLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The language code this explanation is tailored for (e.g., "zh-CN", "en-US"). Required.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 20)]
        public string ExplanationLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The LLM-generated explanation in Markdown format. Required.
        /// </summary>
        [SugarColumn(IsNullable = false, ColumnDataType = "TEXT")] // Ensure TEXT for potentially long markdown
        public string MarkdownExplanation { get; set; } = string.Empty;

        /// <summary>
        /// Phonetic pronunciation (e.g., IPA). Nullable. Generated by LLM.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 512)] // Increased length just in case
        public string? Pronunciation { get; set; }

        /// <summary>
        /// Definitions of the word, potentially in JSON format or structured text, in the ExplanationLanguage. Nullable. Generated by LLM.
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
        public string? Definitions { get; set; }

        /// <summary>
        /// Example sentences using the word, potentially in JSON format or structured text, in the ExplanationLanguage. Nullable. Generated by LLM.
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
        public string? Examples { get; set; }

        /// <summary>
        /// Timestamp when this explanation was created (Unix timestamp in seconds). Required.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; }

        /// <summary>
        /// Identifier of the LLM model used for generation (e.g., "openrouter:meta-llama/llama-4-scout:free"). Nullable.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)]
        public string? ProviderModelName { get; set; }
    }
}