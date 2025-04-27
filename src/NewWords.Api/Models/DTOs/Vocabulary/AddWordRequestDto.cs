using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Vocabulary
{
    /// <summary>
    /// Data Transfer Object for adding a new word to the user's vocabulary list.
    /// </summary>
    public class AddWordRequestDto
    {
        /// <summary>
        /// The text of the word the user wants to add (e.g., "ubiquitous").
        /// The language is inferred from the user's CurrentLearningLanguage.
        /// </summary>
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string WordText { get; set; } = string.Empty;
    }
}