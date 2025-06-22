namespace NewWords.Api.Services.interfaces;

public interface IQueryHistoryService
{
    void LogQueryAsync(long wordCollectionId, int userId);
}
