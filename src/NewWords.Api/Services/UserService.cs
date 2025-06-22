using NewWords.Api.Models.DTOs.User;
using Api.Framework.Models;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Services
{
    public class UserService : IUserService
    {
        private readonly Repositories.IUserRepository _userRepository;

        public UserService(Repositories.IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(long userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                return null;
            }

            // Map Entity to DTO
            return new UserProfileDto
            {
                UserId = user.Id,
                Email = user.Email,
                NativeLanguage = user.NativeLanguage,
                CurrentLearningLanguage = user.CurrentLearningLanguage,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<bool> UpdateUserProfileAsync(long userId, UpdateProfileRequestDto updateDto)
        {
            // Find the user first
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false; // User not found
            }

            // Update allowed fields
            user.NativeLanguage = updateDto.NativeLanguage;
            user.CurrentLearningLanguage = updateDto.CurrentLearningLanguage;

            // Update the entity
            var result = await _userRepository.UpdateAsync(user);

            return result; // Returns true if update was successful
        }

        public async Task<PageData<UserProfileDto>> GetPagedUsersAsync(int pageSize, int pageNumber, bool isAsc = false)
        {
            var userPageData = await _userRepository.GetPagedUsersAsync(pageSize, pageNumber, isAsc);
            var dtoPageData = new PageData<UserProfileDto>
            {
                PageIndex = userPageData.PageIndex,
                PageSize = userPageData.PageSize,
                TotalCount = userPageData.TotalCount,
                DataList = userPageData.DataList?.Select(user => new UserProfileDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    NativeLanguage = user.NativeLanguage,
                    CurrentLearningLanguage = user.CurrentLearningLanguage,
                    CreatedAt = user.CreatedAt
                }).ToList() ?? new List<UserProfileDto>()
            };
            return dtoPageData;
        }
    }
}
