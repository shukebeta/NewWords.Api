using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for user registration requests.
    /// </summary>
    public class RegisterRequestDto
    {
        /// <summary>
        /// User's email address. Must be a valid email format.
        /// </summary>
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's chosen password. Minimum length requirements should be enforced.
        /// </summary>
        [Required]
        [MinLength(6)] // Example: Enforce a minimum password length
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// User's native language code (e.g., "zh-CN", "en-US").
        /// </summary>
        [Required]
        [StringLength(20)]
        public string NativeLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The language the user intends to learn initially (e.g., "en-US", "zh-CN").
        /// </summary>
        [Required]
        [StringLength(20)]
        public string LearningLanguage { get; set; } = string.Empty;
    }
}
