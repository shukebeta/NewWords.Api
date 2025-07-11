using Api.Framework.Models;
using NewWords.Api.Entities;

namespace NewWords.Api.Services.interfaces
{
    public interface IVocabularyService
    {
        Task<PageData<WordExplanation>> GetUserWordsAsync(int userId, int pageSize, int pageNumber);
        Task<WordExplanation> AddUserWordAsync(int userId, string wordText, string learningLanguageCode, string explanationLanguageCode);
        Task DelUserWordAsync(int userId, long wordExplanationId);
        Task<WordExplanation> RefreshUserWordExplanationAsync(long wordExplanationId);
        Task<IList<WordExplanation>> MemoriesAsync(int userId, string localTimezone);
        Task<IList<WordExplanation>> MemoriesOnAsync(int userId, string localTimezone, string yyyyMMdd);
    }
}
