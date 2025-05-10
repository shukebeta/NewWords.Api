using Api.Framework;
using Api.Framework.Models;
using NewWords.Api.Entities;

namespace NewWords.Api.Repositories
{
    public interface IUserRepository : IRepositoryBase<User>
    {
        Task<User?> GetByIdAsync(long userId);
        Task<User?> GetByEmailAsync(string email);
        Task<PageData<User>> GetPagedUsersAsync(int pageSize, int pageNumber, bool isAsc = false);
    }
}
