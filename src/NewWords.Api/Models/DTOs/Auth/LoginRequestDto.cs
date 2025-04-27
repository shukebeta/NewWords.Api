using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for user login requests.
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// User's email address.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password.
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}