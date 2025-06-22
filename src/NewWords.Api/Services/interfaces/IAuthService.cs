using Api.Framework.Models;
using NewWords.Api.Models;
using NewWords.Api.Models.DTOs.Auth;

namespace NewWords.Api.Services.interfaces
{
    /// <summary>
    /// Interface for authentication services.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="request">User registration data.</param>
        /// <param name="jwtConfig"></param>
        /// <returns>UserSession if registration was successful</returns>
        Task<UserSession> RegisterAsync(RegisterRequest request, JwtConfig jwtConfig);

        /// <summary>
        /// Attempts to log in a user.
        /// </summary>
        /// <param name="loginRequest">User login credentials.</param>
        /// <param name="jwtConfig"></param>
        /// <returns>jwtToken if login was successful</returns>
        Task<UserSession> LoginAsync(LoginRequest loginRequest, JwtConfig jwtConfig);
    }
}
