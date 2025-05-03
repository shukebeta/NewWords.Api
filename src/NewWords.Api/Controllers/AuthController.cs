using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Models.DTOs.Auth;
using NewWords.Api.Services;
using Api.Framework.Result;
using NewWords.Api.Helpers;
using EventId = NewWords.Api.Enums.EventId;

namespace NewWords.Api.Controllers
{
    public class AuthController(IAuthService authService) : BaseController
    {
        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="registerDto">The registration details.</param>
        /// <returns>Confirmation of registration.</returns>
        [HttpPost]
        public async Task<ApiResult> Register(RegisterRequestDto registerDto)
        {
            var success = await authService.RegisterAsync(registerDto);
            if (!success)
            {
                return Fail("Registration failed. Email might already be in use.");
            }
            return Success("Registration successful.");
        }

        /// <summary>
        /// Logs in a user and returns a JWT token.
        /// </summary>
        /// <param name="loginDto">The login credentials.</param>
        /// <returns>Authentication response with token.</returns>
        [HttpPost]
        public async Task<ApiResult<AuthResponseDto>> Login(LoginRequestDto loginDto)
        {
            var authResponse = await authService.LoginAsync(loginDto);
            if (authResponse == null)
            {
                throw ExceptionHelper.New(EventId._00100_LoginFailed);
            }
            return new SuccessfulResult<AuthResponseDto>(authResponse, "Login successful.");
        }
    }
}
