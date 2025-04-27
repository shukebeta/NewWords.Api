using Api.Framework;
using NewWords.Api.Entities;
using SqlSugar;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace NewWords.Api.Repositories
{
    public class UserRepository : RepositoryBase<User>, IUserRepository
    {
        public UserRepository(ISqlSugarClient dbClient) : base(dbClient)
        {
        }

        public async Task<User?> GetByIdAsync(int userId)
        {
            return await GetSingleAsync(userId);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await GetFirstOrDefaultAsync(u => u.Email == email);
        }
    }
}