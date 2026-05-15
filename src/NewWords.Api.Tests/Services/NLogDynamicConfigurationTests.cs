using FluentAssertions;
using NewWords.Api.Helpers;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace NewWords.Api.Tests.Services;

public class NLogDynamicConfigurationTests
{
    [Fact]
    public void DescribeRedisConnection_ShouldHideCredentials()
    {
        var description = NLogDynamicConfiguration.DescribeRedisConnection("localhost:6379,password=super-secret,ssl=False");

        description.Should().Contain("localhost:6379");
        description.Should().NotContain("super-secret");
        description.Should().NotContain("password");
    }

    [Fact]
    public void TryApplyMinLevel_ShouldCreateFallbackRuleWithStandardTargets()
    {
        var config = CreateLoggingConfiguration();

        var applied = NLogDynamicConfiguration.TryApplyMinLevel(config, "dynamic-app-logs", LogLevel.Debug);

        applied.Should().BeTrue();
        var rule = config.LoggingRules.Should().ContainSingle(loggingRule => loggingRule.RuleName == "dynamic-app-logs").Subject;
        rule.Targets.Select(target => target.Name).Should().Contain(["file", "console", "seq"]);
        rule.IsLoggingEnabledForLevel(LogLevel.Debug).Should().BeTrue();
        rule.IsLoggingEnabledForLevel(LogLevel.Trace).Should().BeFalse();
    }

    [Fact]
    public void TryApplyMinLevel_ShouldPreserveExistingRuleTargets()
    {
        var config = CreateLoggingConfiguration();
        var existingRule = new LoggingRule("*", LogLevel.Info, config.FindTargetByName("file")!)
        {
            RuleName = "dynamic-app-logs"
        };
        existingRule.Targets.Add(config.FindTargetByName("console")!);
        existingRule.SetLoggingLevels(LogLevel.Info, LogLevel.Fatal);
        config.LoggingRules.Add(existingRule);

        var applied = NLogDynamicConfiguration.TryApplyMinLevel(config, "dynamic-app-logs", LogLevel.Warn);

        applied.Should().BeTrue();
        existingRule.Targets.Select(target => target.Name).Should().Contain(["file", "console"]);
        existingRule.IsLoggingEnabledForLevel(LogLevel.Warn).Should().BeTrue();
        existingRule.IsLoggingEnabledForLevel(LogLevel.Info).Should().BeFalse();
    }

    private static LoggingConfiguration CreateLoggingConfiguration()
    {
        var config = new LoggingConfiguration();
        config.AddTarget("file", new NullTarget("file"));
        config.AddTarget("console", new NullTarget("console"));
        config.AddTarget("seq", new NullTarget("seq"));
        return config;
    }
}
