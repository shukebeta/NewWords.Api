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
using NewWords.Api.Models;
using WeihanLi.Extensions;

namespace NewWords.Api.Services
{
    public class AuthService(Repositories.IUserRepository userRepository, IConfiguration configuration)
        : IAuthService
    {
        public async Task<UserSession> RegisterAsync(RegisterRequest request, JwtConfig jwtConfig)
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

            var claims = TokenHelper.ClaimsGenerator(id, id.ToString(), newUser.Email); // Use newUser.Email for consistency
            var token = TokenHelper.JwtTokenGenerator(claims, jwtConfig.Issuer, jwtConfig.SymmetricSecurityKey, jwtConfig.TokenExpiresInDays);

            // Populate UserId in newUser object after insertion if it's not automatically handled by the ORM
            // Assuming 'id' is the UserId. If newUser object is tracked by ORM and 'id' is assigned to its UserId property, this is fine.
            // For clarity, explicitly assign if needed, e.g., newUser.UserId = id; (if User entity has UserId property)

            return new UserSession
            {
                Token = token,
                UserId = id, // Assuming 'id' is the UserId
                Email = newUser.Email,
                NativeLanguage = newUser.NativeLanguage,
                CurrentLearningLanguage = newUser.CurrentLearningLanguage
            };
        }

        public async Task<UserSession> LoginAsync(LoginRequest loginRequest, JwtConfig jwtConfig)
        {
            // 1. Find user by email
            var user = await userRepository.GetByEmailAsync(loginRequest.Email);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            var validateResult = await _IsValidLogin(loginRequest.Email, loginRequest.Password);
            if (!validateResult.isValidLogin)
            {
                throw new Exception("Username or Password is incorrect");
            }

            if (user.DeletedAt != null)
            {
                throw new Exception("Sorry, your account has been deleted");
            }

            var claims = TokenHelper.ClaimsGenerator(user.UserId, user.UserId.ToString(), user.Email);
            var token = TokenHelper.JwtTokenGenerator(claims, jwtConfig.Issuer, jwtConfig.SymmetricSecurityKey, jwtConfig.TokenExpiresInDays);
            return new UserSession()
            {
                Token = token,
            }.From(validateResult.user);
        }
        private async Task<(bool isValidLogin, User user)> _IsValidLogin(string email, string password)
        {
            var user = await userRepository.GetFirstOrDefaultAsync(x => x.Email == email);
            var isValidLogin = user != null && user.PasswordHash.Equals(CommonHelper.CalculateSha256Hash(password + user.Salt));
            return (isValidLogin, user);
        }
    }
}
