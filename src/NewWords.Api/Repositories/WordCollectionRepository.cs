using Api.Framework;
using NewWords.Api.Entities;
using SqlSugar;

namespace NewWords.Api.Repositories
{
    /// <summary>
    /// Repository implementation for handling WordCollection entities.
    /// </summary>
    public class WordCollectionRepository : RepositoryBase<WordCollection>, IWordCollectionRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WordCollectionRepository"/> class.
        /// </summary>
        /// <param name="dbClient">The SQLSugar client instance.</param>
        public WordCollectionRepository(ISqlSugarClient dbClient) : base(dbClient)
        {
        }

        /// <summary>
        /// Gets a batch of words from the WordCollection table with an ID greater than the specified lastId.
        /// </summary>
        /// <param name="lastId">The ID after which to retrieve words.</param>
        /// <param name="batchSize">The maximum number of words to retrieve.</param>
        /// <returns>A list of WordCollection records.</returns>
        public async Task<List<WordCollection>> GetWordsAfterIdAsync(long lastId, int batchSize)
        {
            // Using the db property from RepositoryBase which provides access to the ISqlSugarClient
            return await db.Queryable<WordCollection>()
                                .Where(wc => wc.Id > lastId)
                                .OrderBy(wc => wc.Id, OrderByType.Asc) // Explicitly specify Ascending order
                                .Take(batchSize)
                                .ToListAsync();
        }
    }
}