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

namespace NewWords.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly Repositories.IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(Repositories.IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<bool> RegisterAsync(RegisterRequestDto registerDto)
        {
            // 1. Check if email already exists
            var existingUser = await _userRepository.GetByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return false; // Email already in use
            }

            // 2. Hash the password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // 3. Create new user entity
            var newUser = new User
            {
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                NativeLanguage = registerDto.NativeLanguage,
                CurrentLearningLanguage = registerDto.LearningLanguage,
                CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds(),
            };

            // 4. Insert user into database
            var result = await _userRepository.InsertAsync(newUser);
            return result; // Return true if insertion was successful
        }

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto)
        {
            // 1. Find user by email
            var user = await _userRepository.GetByEmailAsync(loginDto.Email);
            if (user == null)
            {
                return null; // User not found
            }

            // 2. Verify password
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return null; // Invalid password
            }

            // 3. Generate JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not configured"));
            var issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("JWT Issuer not configured");
            var audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("JWT Audience not configured");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()), // Standard subject claim (user ID)
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token identifier
                    // Add other claims as needed (e.g., roles)
                    // new Claim(ClaimTypes.Role, "User")
                }),
                Expires = DateTime.UtcNow.AddHours(1), // Token expiration (e.g., 1 hour) - make configurable?
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // 4. Return response DTO
            return new AuthResponseDto
            {
                Token = tokenString,
                Expiration = tokenDescriptor.Expires.Value // Use the expiration from the descriptor
            };
        }
    }
}
