using Api.Framework;
using NewWords.Api.Entities;
using SqlSugar;

namespace NewWords.Api.Repositories
{
    public class WordRepository : RepositoryBase<Word>, IWordRepository
    {
        public WordRepository(ISqlSugarClient dbClient) : base(dbClient)
        {
        }

        public async Task<Word?> GetByTextAndLanguageAsync(string wordText, string wordLanguage, string userNativeLanguage)
        {
            return await GetFirstOrDefaultAsync(w => w.WordText == wordText && w.WordLanguage == wordLanguage && w.ExplanationLanguage == userNativeLanguage);
        }
    }
}