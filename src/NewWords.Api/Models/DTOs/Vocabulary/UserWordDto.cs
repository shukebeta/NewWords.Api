using NewWords.Api.Enums;
using System;

namespace NewWords.Api.Models.DTOs.Vocabulary
{
    /// <summary>
    /// Data Transfer Object representing a single word entry in a user's vocabulary list,
    /// including its status and LLM-generated details.
    /// </summary>
    public class UserWordDto
    {
        /// <summary>
        /// Identifier for the user-specific word entry.
        /// </summary>
        public int UserWordId { get; set; }

        /// <summary>
        /// Identifier for the canonical word explanation.
        /// </summary>
        public int WordId { get; set; }

        /// <summary>
        /// The actual word text.
        /// </summary>
        public string WordText { get; set; } = string.Empty;

        /// <summary>
        /// The language code of the word itself (e.g., "en", "zh").
        /// </summary>
        public string WordLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The native language code the explanation is tailored for.
        /// </summary>
        public string UserNativeLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Phonetic pronunciation (e.g., IPA). Nullable.
        /// </summary>
        public string? Pronunciation { get; set; }

        /// <summary>
        /// Definitions of the word in the UserNativeLanguage. Nullable.
        /// </summary>
        public string? Definitions { get; set; }

        /// <summary>
        /// Example sentences in the UserNativeLanguage. Nullable.
        /// </summary>
        public string? Examples { get; set; }

        /// <summary>
        /// Timestamp when the LLM data was generated. Nullable.
        /// </summary>
        public DateTime? GeneratedAt { get; set; }

        /// <summary>
        /// The user's learning status for this word.
        /// </summary>
        public WordStatus Status { get; set; }

        /// <summary>
        /// Timestamp when the user added this word.
        /// </summary>
        public DateTime AddedAt { get; set; }
    }
}