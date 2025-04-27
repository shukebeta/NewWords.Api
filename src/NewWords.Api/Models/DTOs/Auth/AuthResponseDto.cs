namespace NewWords.Api.Models.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for authentication responses (e.g., after successful login).
    /// </summary>
    public class AuthResponseDto
    {
        /// <summary>
        /// The generated JSON Web Token (JWT).
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The expiration date and time of the token (UTC).
        /// </summary>
        public DateTime Expiration { get; set; }

        // Optional: Include some basic user info if needed by the client immediately after login
        // public int UserId { get; set; }
        // public string Email { get; set; } = string.Empty;
    }
}