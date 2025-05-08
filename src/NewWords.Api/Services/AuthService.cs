using NewWords.Api.Models.DTOs.Auth;
using NewWords.Api.Entities;
using SqlSugar;
using System.Threading.Tasks;
using BCrypt.Net; // For password hashing
using Microsoft.Extensions.Configuration; // For reading config
using System.IdentityModel.Tokens.Jwt; // For JWT generation
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;
using Api.Framework.Extensions;
using Api.Framework.Helper;
using Api.Framework.Models;
using WeihanLi.Extensions;

namespace NewWords.Api.Services
{
    public class AuthService(Repositories.IUserRepository userRepository)
        : IAuthService
    {
        public async Task<string> RegisterAsync(RegisterRequest request, JwtConfig jwtConfig)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ArgumentException("Email or Password cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(request.LearningLanguage) || string.IsNullOrWhiteSpace(request.NativeLanguage))
            {
                throw new ArgumentException("Learning Language or native language cannot be empty");
            }

            request.Email = request.Email.Trim().ToLower();
            var existingUser = await userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new Exception($"This Email ({request.Email}) has already registered before");
            }

            var gravatar = GravatarHelper.GetGravatarUrl(request.Email);

            var (salt, password) = CommonHelper.GetSaltedPassword(request.Password);
            var newUser = new User
            {
                Email = request.Email,
                Gravatar = gravatar,
                Salt = salt,
                PasswordHash = password,
                NativeLanguage = request.NativeLanguage,
                CurrentLearningLanguage = request.LearningLanguage,
                CreatedAt = DateTime.Now.ToUnixTimeSeconds(),
            };

            var id = await userRepository.InsertReturnIdentityAsync(newUser);

            var claims = TokenHelper.ClaimsGenerator(id, id.ToString(), request.Email);
            return TokenHelper.JwtTokenGenerator(claims, jwtConfig.Issuer, jwtConfig.SymmetricSecurityKey, jwtConfig.TokenExpiresInDays);
        }

        public async Task<string> LoginAsync(LoginRequest loginRequest, JwtConfig jwtConfig)
        {
            // 1. Find user by email
            var user = await userRepository.GetByEmailAsync(loginRequest.Email);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            var validateResult = await _IsValidLogin(loginRequest.Email, loginRequest.Password);
            if (!validateResult)
            {
                throw new Exception("Username or Password is incorrect");
            }

            if (user.DeletedAt != null)
            {
                throw new Exception("Sorry, your account has been deleted");
            }

            var claims = TokenHelper.ClaimsGenerator(user.UserId, user.UserId.ToString(), user.Email);
            return TokenHelper.JwtTokenGenerator(claims, jwtConfig.Issuer, jwtConfig.SymmetricSecurityKey, jwtConfig.TokenExpiresInDays);
        }
        private async Task<bool> _IsValidLogin(string email, string password)
        {
            var user = await userRepository.GetFirstOrDefaultAsync(x => x.Email == email);
            return user != null && user.PasswordHash.Equals(CommonHelper.CalculateSha256Hash(password + user.Salt));
        }
    }
}
