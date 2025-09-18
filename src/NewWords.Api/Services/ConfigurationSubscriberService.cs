using Microsoft.Extensions.Options;
using NewWords.Api.Options;
using NLog;
using NLog.Config;
using StackExchange.Redis;

namespace NewWords.Api.Services;

public class ConfigurationSubscriberService : BackgroundService
{
    private readonly ILogger<ConfigurationSubscriberService> _logger;
    private readonly RedisOptions _redisOptions;
    private ConnectionMultiplexer? _redis;
    private ISubscriber? _subscriber;

    public ConfigurationSubscriberService(
        ILogger<ConfigurationSubscriberService> logger,
        IOptions<RedisOptions> redisOptions)
    {
        _logger = logger;
        _redisOptions = redisOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Configuration Subscriber Service starting");

        try
        {
            await ConnectToRedis();
            await SubscribeToConfigChanges(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Configuration Subscriber Service");
        }
    }

    private async Task ConnectToRedis()
    {
        _logger.LogInformation("Connecting to Redis: {ConnectionString}", _redisOptions.ConnectionString);
        
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisOptions.ConnectionString);
        _subscriber = _redis.GetSubscriber();
        
        _logger.LogInformation("Connected to Redis successfully");
    }

    private async Task SubscribeToConfigChanges(CancellationToken stoppingToken)
    {
        var pattern = $"{_redisOptions.ProjectPrefix}:config:*";
        _logger.LogInformation("Subscribing to Redis pattern: {Pattern}", pattern);

        await _subscriber!.SubscribeAsync(
            new RedisChannel(pattern, RedisChannel.PatternMode.Pattern),
            (channel, value) =>
            {
                try
                {
                    HandleConfigChange(channel, value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling config change for channel: {Channel}", channel);
                }
            });

        _logger.LogInformation("Subscribed to config changes. Waiting for messages...");
        
        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Configuration Subscriber Service stopping");
        }
    }

    private void HandleConfigChange(RedisChannel channel, RedisValue value)
    {
        var channelName = channel.ToString();
        var configValue = value.ToString();
        
        _logger.LogInformation("Received config change: {Channel} = {Value}", channelName, configValue);

        // Parse the config key: "newwords:config:category:setting"
        var parts = channelName.Split(':');
        if (parts.Length < 4)
        {
            _logger.LogWarning("Invalid config key format: {Channel}", channelName);
            return;
        }

        var category = parts[2];
        var setting = parts[3];

        switch (category.ToLower())
        {
            case "nlog":
                HandleNLogConfig(setting, configValue);
                break;
            default:
                _logger.LogWarning("Unknown config category: {Category}", category);
                break;
        }
    }

    private void HandleNLogConfig(string setting, string value)
    {
        switch (setting.ToLower())
        {
            case "minlevel":
                UpdateNLogMinLevel(value);
                break;
            default:
                _logger.LogWarning("Unknown NLog setting: {Setting}", setting);
                break;
        }
    }

    private void UpdateNLogMinLevel(string levelString)
    {
        try
        {
            var newLevel = NLog.LogLevel.FromString(levelString);
            if (newLevel != null)
            {
                var config = LogManager.Configuration;
                if (config?.LoggingRules?.Count > 0)
                {
                    // Update the main logging rule (the last one, like in the existing NLog.config)
                    var mainRule = config.LoggingRules.LastOrDefault();
                    if (mainRule != null)
                    {
                        mainRule.SetLoggingLevels(newLevel, NLog.LogLevel.Fatal);
                        LogManager.ReconfigExistingLoggers();
                        
                        _logger.LogInformation("NLog minimum level updated to: {Level}", newLevel);
                        
                        // Test the new log level
                        TestLogLevels();
                    }
                    else
                    {
                        _logger.LogWarning("No main logging rule found in NLog configuration");
                    }
                }
                else
                {
                    _logger.LogWarning("No logging rules found in NLog configuration");
                }
            }
            else
            {
                _logger.LogWarning("Invalid log level: {Level}. Valid values: Trace, Debug, Info, Warn, Error, Fatal", levelString);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating NLog minimum level to: {Level}", levelString);
        }
    }

    private void TestLogLevels()
    {
        var nlogLogger = LogManager.GetCurrentClassLogger();
        nlogLogger.Trace("TEST: This is a TRACE message after level update");
        nlogLogger.Debug("TEST: This is a DEBUG message after level update");
        nlogLogger.Info("TEST: This is an INFO message after level update");
        nlogLogger.Warn("TEST: This is a WARN message after level update");
        nlogLogger.Error("TEST: This is an ERROR message after level update");
        nlogLogger.Fatal("TEST: This is a FATAL message after level update");
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Configuration Subscriber Service is stopping");
        
        _redis?.Dispose();
        
        await base.StopAsync(stoppingToken);
    }
}