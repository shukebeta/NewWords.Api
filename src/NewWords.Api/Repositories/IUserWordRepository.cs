using Api.Framework;
using NewWords.Api.Entities;
using NewWords.Api.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NewWords.Api.Repositories
{
    public interface IUserWordRepository : IRepositoryBase<UserWord>
    {
        Task<UserWord?> GetByUserAndWordIdAsync(long userId, int wordId);
        Task<List<UserWord>> GetUserWordsAsync(long userId, WordStatus? status, int page, int pageSize);
        Task<int> GetUserWordsCountAsync(long userId, WordStatus? status);
    }
}
