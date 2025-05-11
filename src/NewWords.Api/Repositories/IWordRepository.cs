using Api.Framework;
using NewWords.Api.Entities;

namespace NewWords.Api.Repositories
{
    public interface IWordRepository : IRepositoryBase<WordExplanation>
    {
        // This method's signature might need to change based on new querying needs for WordExplanation
        // For now, let's assume it might be adapted or removed if FillWordsTable uses direct queries.
        // Task<WordExplanation?> GetByTextAndLanguageAsync(string wordText, string wordLanguage, string explanationLanguage);
        // For FillWordsTable, we'll likely query WordExplanation by WordCollectionId and ExplanationLanguage directly.
    }
}