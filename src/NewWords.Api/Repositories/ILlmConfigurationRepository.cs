using Api.Framework;
using NewWords.Api.Entities;

namespace NewWords.Api.Repositories
{
    public interface ILlmConfigurationRepository : IRepositoryBase<LlmConfiguration>
    {
        Task<LlmConfiguration?> GetByIdAsync(int id);
    }
}