using SqlSugar;
using System;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents a user account.
    /// </summary>
    [SugarTable("Users")] // Maps to the "Users" table
    public class User
    {
        /// <summary>
        /// Unique identifier for the user (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long UserId { get; set; }

        /// <summary>
        /// User's email address (Unique, Required). Used for login.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Hashed password for the user (Required).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// User's native language code (e.g., "zh-CN", "en-US") (Required).
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 10)] // Added length
        public string NativeLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The language the user is currently learning (e.g., "en-US", "zh-CN") (Required).
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 10)] // Added length
        public string CurrentLearningLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the user account was created (Required, Unix timestamp as long).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; } = 0; // Default to 0 (Unix epoch start)
    }
}
