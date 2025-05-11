using Api.Framework;
using NewWords.Api.Entities;
using SqlSugar;

namespace NewWords.Api.Repositories
{
    public class WordRepository : RepositoryBase<WordExplanation>, IWordRepository
    {
        public WordRepository(ISqlSugarClient dbClient) : base(dbClient)
        {
        }

        // The GetByTextAndLanguageAsync method is removed as its direct utility for WordExplanation
        // in the context of FillWordsTable is unclear. Direct queries will be used in the controller.
        // If a similar method is needed for WordExplanation, it can be added later with a revised signature.
    }
}