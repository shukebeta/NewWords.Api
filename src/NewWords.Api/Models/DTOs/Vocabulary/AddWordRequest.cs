using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Vocabulary
{
    public class AddWordRequest
    {
        [Required]
        [StringLength(255)]
        public string WordText { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string LearningLanguage { get; set; } = string.Empty; // User's learning language (e.g., "en")

        [Required]
        [StringLength(20)]
        public string ExplanationLanguage { get; set; } = string.Empty; // User's native language for the explanation (e.g., "zh-CN")
    }
}
