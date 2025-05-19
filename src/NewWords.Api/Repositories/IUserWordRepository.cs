using Api.Framework;
using NewWords.Api.Entities;
using NewWords.Api.Enums;

namespace NewWords.Api.Repositories
{
    public interface IUserWordRepository : IRepositoryBase<UserWord>
    {
        Task<UserWord?> GetByUserAndWordIdAsync(long userId, int wordId);
        Task<List<UserWord>> GetUserWordsAsync(long userId, FamiliarityLevel? status, int page, int pageSize);
        Task<int> GetUserWordsCountAsync(long userId, FamiliarityLevel? status);
    }
}
