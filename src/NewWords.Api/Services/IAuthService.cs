using NewWords.Api.Models.DTOs.Auth;
using System.Threading.Tasks;

namespace NewWords.Api.Services
{
    /// <summary>
    /// Interface for authentication services.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="registerDto">User registration data.</param>
        /// <returns>True if registration was successful, false otherwise (e.g., email exists).</returns>
        Task<bool> RegisterAsync(RegisterRequestDto registerDto);

        /// <summary>
        /// Attempts to log in a user.
        /// </summary>
        /// <param name="loginDto">User login credentials.</param>
        /// <returns>An AuthResponseDto containing the JWT if login is successful, otherwise null.</returns>
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto);
    }
}