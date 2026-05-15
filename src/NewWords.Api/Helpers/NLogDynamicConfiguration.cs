using NLog;
using NLog.Config;
using NLog.Targets;
using StackExchange.Redis;

namespace NewWords.Api.Helpers;

internal static class NLogDynamicConfiguration
{
    private static readonly string[] DefaultTargetNames = ["file", "console", "seq"];

    internal static string DescribeRedisConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "not configured";
        }

        try
        {
            var options = ConfigurationOptions.Parse(connectionString);
            var endpoints = options.EndPoints
                .Select(endpoint => endpoint.ToString())
                .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint))
                .Distinct()
                .ToArray();

            return endpoints.Length > 0 ? string.Join(", ", endpoints) : "configured";
        }
        catch
        {
            return "configured";
        }
    }

    internal static bool TryApplyMinLevel(LoggingConfiguration config, string ruleName, NLog.LogLevel minLevel)
    {
        var dynamicRule = config.LoggingRules.FirstOrDefault(rule => rule.RuleName == ruleName);
        if (dynamicRule == null)
        {
            dynamicRule = CreateFallbackRule(config, ruleName, minLevel);
            if (dynamicRule == null)
            {
                return false;
            }

            config.LoggingRules.Add(dynamicRule);
        }
        else
        {
            dynamicRule.SetLoggingLevels(minLevel, NLog.LogLevel.Fatal);
        }

        return true;
    }

    private static LoggingRule? CreateFallbackRule(LoggingConfiguration config, string ruleName, NLog.LogLevel minLevel)
    {
        var targets = DefaultTargetNames
            .Select(config.FindTargetByName)
            .Where(target => target != null)
            .Cast<Target>()
            .ToArray();

        if (targets.Length == 0)
        {
            return null;
        }

        var dynamicRule = new LoggingRule("*", minLevel, targets[0])
        {
            RuleName = ruleName
        };

        for (var i = 1; i < targets.Length; i++)
        {
            dynamicRule.Targets.Add(targets[i]);
        }

        dynamicRule.SetLoggingLevels(minLevel, NLog.LogLevel.Fatal);
        return dynamicRule;
    }
}
