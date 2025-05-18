using System;

namespace NewWords.Api.Entities;

public class QueryHistory
{
    public long Id { get; set; }
    public long WordCollectionId { get; set; }
    public long UserId { get; set; }
    public long CreatedAt { get; set; }
}
