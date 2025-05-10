using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Vocabulary
{
    public class AddWordRequestDto
    {
        [Required]
        [StringLength(255)]
        public string WordText { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string WordLanguage { get; set; } = string.Empty; // Language of the word itself (e.g., "en")

        [Required]
        [StringLength(20)]
        public string ExplanationLanguage { get; set; } = string.Empty; // User's native language for the explanation (e.g., "zh-CN")

        [Required]
        public string MarkdownExplanation { get; set; } = string.Empty;
        
        // Optional fields from Word entity that might be provided at creation
        public string? Pronunciation { get; set; }
        public string? Definitions { get; set; } // JSON or structured text
        public string? Examples { get; set; }    // JSON or structured text
        public string? ProviderModelName { get; set; }
    }
}