using Api.Framework;
using NewWords.Api.Entities;
using NewWords.Api.Enums;
using SqlSugar;

namespace NewWords.Api.Repositories
{
    public class UserWordRepository(ISqlSugarClient dbClient) : RepositoryBase<UserWord>(dbClient), IUserWordRepository
    {
        public async Task<UserWord?> GetByUserAndWordIdAsync(long userId, int wordId)
        {
            return await GetFirstOrDefaultAsync(uw => uw.UserId == userId && uw.WordExplanationId == wordId);
        }

        public async Task<List<UserWord>> GetUserWordsAsync(long userId, FamiliarityLevel? status, int page, int pageSize)
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

        public async Task<int> GetUserWordsCountAsync(long userId, FamiliarityLevel? status)
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
