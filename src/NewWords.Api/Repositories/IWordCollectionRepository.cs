using Api.Framework;
using NewWords.Api.Entities;

namespace NewWords.Api.Repositories
{
    /// <summary>
    /// Interface for repository handling WordCollection entities.
    /// </summary>
    public interface IWordCollectionRepository : IRepositoryBase<WordCollection>
    {
        /// <summary>
        /// Gets a batch of words from the WordCollection table with an ID greater than the specified lastId.
        /// </summary>
        /// <param name="lastId">The ID after which to retrieve words.</param>
        /// <param name="batchSize">The maximum number of words to retrieve.</param>
        /// <returns>A list of WordCollection records.</returns>
        Task<List<WordCollection>> GetWordsAfterIdAsync(long lastId, int batchSize);
    }
}