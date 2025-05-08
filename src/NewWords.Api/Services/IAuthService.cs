using NewWords.Api.Models.DTOs.Auth;
using System.Threading.Tasks;
using Api.Framework.Models;

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
        /// <param name="request">User registration data.</param>
        /// <param name="jwtConfig"></param>
        /// <returns>jwtToken if registration was successful</returns>
        Task<string> RegisterAsync(RegisterRequest request, JwtConfig jwtConfig);

        /// <summary>
        /// Attempts to log in a user.
        /// </summary>
        /// <param name="loginRequest">User login credentials.</param>
        /// <param name="jwtConfig"></param>
        /// <returns>jwtToken if login was successful</returns>
        Task<string> LoginAsync(LoginRequest loginRequest, JwtConfig jwtConfig);
    }
}
