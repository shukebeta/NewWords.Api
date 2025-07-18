using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.User
{
    /// <summary>
    /// Data Transfer Object for updating a user's language preferences.
    /// </summary>
    public class UpdateLanguagesRequestDto
    {
        /// <summary>
        /// User's native language code (e.g., "zh-CN", "en-US").
        /// </summary>
        [Required]
        [StringLength(10)]
        public string NativeLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The language the user is learning (e.g., "en-US", "zh-CN").
        /// </summary>
        [Required]
        [StringLength(10)]
        public string LearningLanguage { get; set; } = string.Empty;
    }
}