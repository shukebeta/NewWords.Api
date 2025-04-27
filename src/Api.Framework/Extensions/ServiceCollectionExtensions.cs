using System.Text.RegularExpressions;
using Api.Framework.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using DbType = System.Data.DbType;

namespace Api.Framework.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddSqlSugarSetup(this IServiceCollection services, DatabaseConnectionOptions options,
        ILogger logger)

    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        services.AddSingleton<ISqlSugarClient>(_ =>
        {
            var client = new SqlSugarScope(new ConnectionConfig
                {
                    ConnectionString = options.ConnectionString, // Required
                    DbType = SqlSugar.DbType.MySql, // Required
                    IsAutoCloseConnection = true,
                    InitKeyType = InitKeyType.Attribute
                },
                db =>
                {
                    db.Aop.OnLogExecuting = (sql, pars) =>
                    {
                        if (_IsProductionEnv()) return;
                        logger.LogInformation(db.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName,
                            it => it.Value)));
                        foreach (var p in pars)
                        {
                            string v;
                            if (p.Value == null)
                            {
                                v = "null";
                                sql = sql.Replace(p.ParameterName, v);
                                continue;
                            }

                            switch (p.DbType)
                            {
                                case DbType.Byte:
                                case DbType.SByte:
                                case DbType.Single:
                                case DbType.Double:
                                case DbType.UInt16:
                                case DbType.UInt32:
                                case DbType.UInt64:
                                case DbType.Int16:
                                case DbType.Int32:
                                case DbType.Int64:
                                case DbType.VarNumeric:
                                    v = p.Value!.ToString()!;
                                    break;
                                case DbType.Boolean:
                                    v = p.Value.Equals(true) ? "true" : "false";
                                    break;
                                case DbType.DateTime:
                                case DbType.DateTime2:
                                case DbType.DateTimeOffset:
                                    v = $"'{((DateTime) p.Value):yyyy-MM-dd HH:mm:ss}'";
                                    break;
                                default:
                                    string strValue = p.Value?.ToString() ?? "";
                                    if (strValue.Contains(@"'"))
                                        strValue = Regex.Replace(strValue, @"'", @"\'");
                                    v = $"'{strValue}'";
                                    break;
                            }

                            sql = Regex.Replace(sql, $@"{p.ParameterName}\b", $"{v}");
                        }

                        logger.LogInformation(sql);
                    };
                });

            return client;
        });
    }

    private static bool _IsProductionEnv()
    {
        return "Production".Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            StringComparison.InvariantCulture);
    }
}
