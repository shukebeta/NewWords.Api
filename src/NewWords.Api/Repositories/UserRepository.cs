using Api.Framework;
using Api.Framework.Models;
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

        public async Task<PageData<User>> GetPagedUsersAsync(int pageSize, int pageNumber, bool isAsc = false)
        {
            var pageData = new PageData<User>
            {
                PageIndex = pageNumber,
                PageSize = pageSize,
            };
            RefAsync<int> totalCount = 0;
            var result = await db.Queryable<User>()
                .OrderBy(u => u.UserId, isAsc ? OrderByType.Asc : OrderByType.Desc)
                .ToPageListAsync(pageNumber, pageSize, totalCount);
            pageData.TotalCount = totalCount;
            pageData.DataList = result;
            return pageData;
        }
    }
}