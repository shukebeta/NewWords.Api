using Api.Framework.Models;
using NewWords.Api.Entities;
using System.Threading.Tasks;

namespace NewWords.Api.Services
{
    public interface IVocabularyService
    {
        Task<PageData<WordExplanation>> GetUserWordsAsync(int userId, int pageSize, int pageNumber);
        Task<WordExplanation> AddUserWordAsync(int userId, string wordText, string wordLanguageCode, string explanationLanguageCode);
        Task DelUserWordAsync(int userId, long wordExplanationId);
    }
}
