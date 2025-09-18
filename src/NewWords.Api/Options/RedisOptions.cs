namespace NewWords.Api.Options;

public class RedisOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ProjectPrefix { get; set; } = string.Empty;
}