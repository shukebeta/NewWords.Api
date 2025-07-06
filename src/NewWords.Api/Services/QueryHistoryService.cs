using System;
using System.Threading.Tasks;
using Api.Framework;
using Api.Framework.Extensions;
using Microsoft.Extensions.Logging;
using NewWords.Api.Entities;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Services;

public class QueryHistoryService(
    IRepositoryBase<QueryHistory> repo,
    ILogger<QueryHistoryService> logger)
    : IQueryHistoryService
{
    public void LogQueryAsync(long wordCollectionId, int userId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await repo.InsertAsync(new QueryHistory
                {
                    WordCollectionId = wordCollectionId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow.ToUnixTimeSeconds()
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to log query history for wordCollectionId: {WordCollectionId}", wordCollectionId);
            }
        });
    }
}
