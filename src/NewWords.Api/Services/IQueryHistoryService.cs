namespace NewWords.Api.Services;

public interface IQueryHistoryService
{
    void LogQueryAsync(long wordCollectionId, int userId);
}
