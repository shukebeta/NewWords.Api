using Api.Framework;
using NewWords.Api.Entities;
using SqlSugar;

namespace NewWords.Api.Repositories
{
    public class LlmConfigurationRepository : RepositoryBase<LlmConfiguration>, ILlmConfigurationRepository
    {
        public LlmConfigurationRepository(ISqlSugarClient dbClient) : base(dbClient)
        {
        }

        public async Task<LlmConfiguration?> GetByIdAsync(int id)
        {
            return await GetSingleAsync(id);
        }
    }
}