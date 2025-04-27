using Api.Framework;
using NewWords.Api.Entities;
using System.Threading.Tasks;

namespace NewWords.Api.Repositories
{
    public interface IUserRepository : IRepositoryBase<User>
    {
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByEmailAsync(string email);
    }
}