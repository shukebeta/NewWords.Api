namespace Api.Framework.Database;

public class DatabaseConnectionOptions
{
    public string? ConnectionString { get;  init; }

    public DatabaseType DbType { get; init; }

}