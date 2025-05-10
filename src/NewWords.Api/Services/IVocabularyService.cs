using Api.Framework.Models;
using NewWords.Api.Entities;
using System.Threading.Tasks;

namespace NewWords.Api.Services
{
    public interface IVocabularyService
    {
        Task<PageData<Word>> GetUserWordsAsync(long userId, int pageSize, int pageNumber);
        Task<Word> AddUserWordAsync(long userId, Word word); // Changed to return Word for the Add endpoint
    }
}
