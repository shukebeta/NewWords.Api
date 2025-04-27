using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewWords.Api.Models.DTOs.Auth;
using NewWords.Api.Services;
using Api.Framework.Result;

namespace NewWords.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registers a new user.
        /// </summary>
        /// <param name="registerDto">The registration details.</param>
        /// <returns>Confirmation of registration.</returns>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerDto)
        {
            var success = await _authService.RegisterAsync(registerDto);
            if (!success)
            {
                return BadRequest(new FailedResult("Registration failed. Email might already be in use."));
            }
            return Ok(new SuccessfulResult<string>("Registration successful."));
        }

        /// <summary>
        /// Logs in a user and returns a JWT token.
        /// </summary>
        /// <param name="loginDto">The login credentials.</param>
        /// <returns>Authentication response with token.</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
        {
            var authResponse = await _authService.LoginAsync(loginDto);
            if (authResponse == null)
            {
                return Unauthorized(new FailedResult("Invalid credentials."));
            }
            return Ok(new SuccessfulResult<AuthResponseDto>(authResponse, "Login successful."));
        }
    }
}