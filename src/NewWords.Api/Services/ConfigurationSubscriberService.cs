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
    private readonly object _nlogLock = new object();
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectToRedis();
                await SubscribeToConfigChanges(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Configuration Subscriber Service. Retrying in 15 seconds...");
                
                // Dispose existing connection if any
                _redis?.Dispose();
                _redis = null;
                _subscriber = null;
                
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Configuration Subscriber Service stopping during retry delay");
                    break;
                }
            }
        }
    }

    private async Task ConnectToRedis()
    {
        _logger.LogInformation("Connecting to Redis: {ConnectionString}", _redisOptions.ConnectionString);
        
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisOptions.ConnectionString);
        _subscriber = _redis.GetSubscriber();
        
        _logger.LogInformation("Connected to Redis successfully");
        
        // Load current configuration values on startup
        await LoadCurrentConfigValues();
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
            // Validate input
            if (string.IsNullOrWhiteSpace(levelString))
            {
                _logger.LogWarning("Cannot update NLog level: empty or null level string provided, keeping current configuration");
                return;
            }

            var newLevel = NLog.LogLevel.FromString(levelString);
            if (newLevel != null)
            {
                lock (_nlogLock)
                {
                    var config = LogManager.Configuration;
                    if (config == null)
                    {
                        _logger.LogError("NLog configuration is null");
                        return;
                    }

                    // Check if seq target exists - without centralized logging, dynamic adjustment is pointless
                    var seqTarget = config.FindTargetByName("seq");
                    if (seqTarget == null)
                    {
                        _logger.LogError("Cannot update log level: No 'seq' target found. Dynamic log level adjustment requires centralized logging to be useful.");
                        return;
                    }

                    // Find or create the dynamic logging rule
                    var dynamicRule = config.LoggingRules.FirstOrDefault(r => r.RuleName == "dynamic-app-logs");
                    if (dynamicRule == null)
                    {
                        // Create new rule with seq target only
                        dynamicRule = new NLog.Config.LoggingRule("*", seqTarget);
                        dynamicRule.RuleName = "dynamic-app-logs";
                        dynamicRule.SetLoggingLevels(newLevel, NLog.LogLevel.Fatal);
                        config.LoggingRules.Add(dynamicRule);
                        
                        _logger.LogInformation("Created new dynamic logging rule for centralized logging with level: {Level}", newLevel);
                    }
                    else
                    {
                        dynamicRule.SetLoggingLevels(newLevel, NLog.LogLevel.Fatal);
                        _logger.LogInformation("Updated dynamic logging rule level to: {Level}", newLevel);
                    }

                    LogManager.ReconfigExistingLoggers();
                    
                    // Test the new log level
                    TestLogLevels();
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

    private async Task LoadCurrentConfigValues()
    {
        try
        {
            _logger.LogInformation("Loading current configuration values from Redis");
            
            var database = _redis!.GetDatabase();
            
            // Load NLog level configuration
            var nlogLevelKey = $"{_redisOptions.ProjectPrefix}:config:nlog:minlevel";
            var currentLogLevel = await database.StringGetAsync(nlogLevelKey);
            
            if (currentLogLevel.HasValue && !string.IsNullOrWhiteSpace(currentLogLevel))
            {
                _logger.LogInformation("Found existing NLog level configuration: {Level}", currentLogLevel);
                UpdateNLogMinLevel(currentLogLevel!);
            }
            else
            {
                if (currentLogLevel.HasValue)
                {
                    _logger.LogWarning("Found empty/whitespace NLog level configuration in Redis, using default");
                }
                else
                {
                    _logger.LogInformation("No existing NLog level configuration found in Redis, using default");
                }
            }
            
            // Future: Add other configuration loading here
            // var llmTimeoutKey = $"{_redisOptions.ProjectPrefix}:config:llm:timeout";
            // var llmTimeout = await database.StringGetAsync(llmTimeoutKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading current configuration values from Redis");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Configuration Subscriber Service is stopping");
        
        _redis?.Dispose();
        
        await base.StopAsync(stoppingToken);
    }
}