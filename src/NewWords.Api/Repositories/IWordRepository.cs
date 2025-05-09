using Api.Framework;
using NewWords.Api.Entities;
using System.Threading.Tasks;

namespace NewWords.Api.Repositories
{
    public interface IWordRepository : IRepositoryBase<Word>
    {
        Task<Word?> GetByTextAndLanguageAsync(string wordText, string wordLanguage, string userNativeLanguage);
    }
}