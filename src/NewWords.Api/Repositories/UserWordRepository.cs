using Api.Framework;
using NewWords.Api.Entities;
using NewWords.Api.Enums;
using SqlSugar;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NewWords.Api.Repositories
{
    public class UserWordRepository : RepositoryBase<UserWord>, IUserWordRepository
    {
        public UserWordRepository(ISqlSugarClient dbClient) : base(dbClient)
        {
        }

        public async Task<UserWord?> GetByUserAndWordIdAsync(int userId, int wordId)
        {
            return await GetFirstOrDefaultAsync(uw => uw.UserId == userId && uw.WordId == wordId);
        }

        public async Task<List<UserWord>> GetUserWordsAsync(int userId, WordStatus? status, int page, int pageSize)
        {
            var query = db.Queryable<UserWord>()
                .Where(uw => uw.UserId == userId);

            if (status.HasValue)
            {
                query = query.Where(uw => uw.Status == status.Value);
            }

            page = System.Math.Max(1, page);
            pageSize = System.Math.Clamp(pageSize, 1, 100);

            return await query
                .OrderBy(uw => uw.CreatedAt, OrderByType.Desc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetUserWordsCountAsync(int userId, WordStatus? status)
        {
            var query = db.Queryable<UserWord>()
                .Where(uw => uw.UserId == userId);

            if (status.HasValue)
            {
                query = query.Where(uw => uw.Status == status.Value);
            }

            return await query.CountAsync();
        }
    }
}