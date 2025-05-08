using Api.Framework.Models;
using Microsoft.AspNetCore.Mvc;
using NewWords.Api.Models.DTOs.Auth;
using NewWords.Api.Services;
using Api.Framework.Result;
using Microsoft.Extensions.Options;

namespace NewWords.Api.Controllers;

public class AuthController(IAuthService authService, IOptions<JwtConfig> jwtConfig) : BaseController
{
    private readonly JwtConfig _jwtConfig = jwtConfig.Value;

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="register">The registration details.</param>
    /// <returns>Jwt token if successful</returns>
    [HttpPost]
    public async Task<ApiResult<JwtToken>> Register(RegisterRequest register)
    {
        var token = await authService.RegisterAsync(register, _jwtConfig);
        return new SuccessfulResult<JwtToken>(new JwtToken {Token = token,});
    }

    /// <summary>
    /// Logs in a user and returns a JWT token.
    /// </summary>
    /// <param name="loginRequest">The login credentials.</param>
    /// <returns>Jwt token if successful</returns>
    [HttpPost]
    public async Task<ApiResult<JwtToken>> Login(LoginRequest loginRequest)
    {
        if (string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
        {
            throw new ArgumentException("Email or Password cannot be empty");
        }
        var jwtToken = await authService.LoginAsync(loginRequest, _jwtConfig);
        return new SuccessfulResult<JwtToken>(new JwtToken {Token = jwtToken,});
    }
}
