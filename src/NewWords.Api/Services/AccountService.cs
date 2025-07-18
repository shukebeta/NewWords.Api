using NewWords.Api.Models.DTOs.User;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Services
{
    public class AccountService : IAccountService
    {
        private readonly Repositories.IUserRepository _userRepository;

        public AccountService(Repositories.IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> UpdateUserLanguagesAsync(long userId, UpdateLanguagesRequestDto updateDto)
        {
            // Find the user first
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false; // User not found
            }

            // Update language fields
            user.NativeLanguage = updateDto.NativeLanguage;
            user.CurrentLearningLanguage = updateDto.LearningLanguage;

            // Update the entity
            var result = await _userRepository.UpdateAsync(user);

            return result; // Returns true if update was successful
        }
    }
}