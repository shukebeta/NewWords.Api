using System;
using System.Threading.Tasks;
using SqlSugar;
using Microsoft.Extensions.Logging;

namespace Api.Framework
{
    public static class TransactionHelper
    {
        public static async Task ExecuteInTransactionAsync(ISqlSugarClient db, Func<Task> action, ILogger? logger = null)
        {
            try
            {
                await db.AsTenant().BeginTranAsync();
                await action();
                await db.AsTenant().CommitTranAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    await db.AsTenant().RollbackTranAsync();
                }
                catch (Exception rbEx)
                {
                    logger?.LogError(rbEx, "Rollback failed after transaction exception");
                }
                logger?.LogError(ex, "Transaction action threw an exception");
                throw;
            }
        }

        public static async Task<T> ExecuteInTransactionAsync<T>(ISqlSugarClient db, Func<Task<T>> func, ILogger? logger = null)
        {
            try
            {
                await db.AsTenant().BeginTranAsync();
                var result = await func();
                await db.AsTenant().CommitTranAsync();
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    await db.AsTenant().RollbackTranAsync();
                }
                catch (Exception rbEx)
                {
                    logger?.LogError(rbEx, "Rollback failed after transaction exception");
                }
                logger?.LogError(ex, "Transaction function threw an exception");
                throw;
            }
        }
    }
}
