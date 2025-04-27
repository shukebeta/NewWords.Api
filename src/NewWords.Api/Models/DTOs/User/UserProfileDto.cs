namespace NewWords.Api.Models.DTOs.User
{
    /// <summary>
    /// Data Transfer Object representing a user's profile information.
    /// </summary>
    public class UserProfileDto
    {
        /// <summary>
        /// User's unique identifier.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// User's email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's native language code (e.g., "zh-CN", "en-US").
        /// </summary>
        public string NativeLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The language the user is currently learning (e.g., "en-US", "zh-CN").
        /// </summary>
        public string CurrentLearningLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the user account was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}