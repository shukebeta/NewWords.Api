using Api.Framework.Models;
using NewWords.Api.Entities;
using System.Threading.Tasks;

namespace NewWords.Api.Services
{
    public interface IVocabularyService
    {
        Task<PageData<WordExplanation>> GetUserWordsAsync(long userId, int pageSize, int pageNumber);
        Task<WordExplanation> AddUserWordAsync(long userId, string wordText, string wordLanguage, string explanationLanguage);
    }
}
