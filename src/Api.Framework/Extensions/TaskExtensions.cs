using Microsoft.Extensions.Logging;

namespace Api.Framework.Extensions;

/// <summary>
/// Extension methods for safe fire-and-forget task execution
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Execute a task in fire-and-forget manner with proper exception handling
    /// </summary>
    /// <param name="taskFunc">Async function to execute</param>
    /// <param name="logger">Logger for exception handling</param>
    /// <param name="errorMessage">Custom error message (optional)</param>
    public static void SafeFireAndForget(Func<Task> taskFunc, ILogger? logger = null, string? errorMessage = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await taskFunc();
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    var message = errorMessage ?? "Fire-and-forget task failed";
                    logger.LogError(ex, "{ErrorMessage}", message);
                }
                // Intentionally suppress exception to prevent affecting the main flow
            }
        });
    }

    /// <summary>
    /// Execute multiple sync operations in fire-and-forget manner with proper exception handling
    /// </summary>
    /// <typeparam name="T">Type of sync service</typeparam>
    /// <param name="syncServices">Collection of sync services</param>
    /// <param name="syncAction">Action to execute on each service</param>
    /// <param name="logger">Logger for exception handling</param>
    /// <param name="errorMessage">Custom error message (optional)</param>
    public static void SafeFireAndForgetBatch<T>(
        IEnumerable<T> syncServices, 
        Func<T, Task> syncAction, 
        ILogger? logger = null, 
        string? errorMessage = null)
    {
        foreach (var syncService in syncServices)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await syncAction(syncService);
                }
                catch (Exception ex)
                {
                    if (logger != null)
                    {
                        var message = errorMessage ?? $"Fire-and-forget sync operation failed for service {typeof(T).Name}";
                        logger.LogError(ex, "{ErrorMessage}", message);
                    }
                    // Intentionally suppress exception to prevent affecting the main flow
                }
            });
        }
    }
}