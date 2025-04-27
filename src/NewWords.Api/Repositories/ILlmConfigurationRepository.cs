using Api.Framework;
using NewWords.Api.Entities;
using System.Threading.Tasks;

namespace NewWords.Api.Repositories
{
    public interface ILlmConfigurationRepository : IRepositoryBase<LlmConfiguration>
    {
        Task<LlmConfiguration?> GetByIdAsync(int id);
    }
}