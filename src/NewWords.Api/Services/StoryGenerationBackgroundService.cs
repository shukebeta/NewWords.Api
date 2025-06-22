using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Services
{
    public class StoryGenerationBackgroundService : BackgroundService
    {
        private readonly ILogger<StoryGenerationBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public StoryGenerationBackgroundService(
            ILogger<StoryGenerationBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Story Generation Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var nextRun = DateTime.Today.AddDays(1).AddHours(2); // 2 AM next day
                    
                    if (now.Hour >= 2) // If it's already past 2 AM today, schedule for tomorrow
                    {
                        nextRun = DateTime.Today.AddDays(1).AddHours(2);
                    }
                    else // If it's before 2 AM today, schedule for today
                    {
                        nextRun = DateTime.Today.AddHours(2);
                    }

                    var delay = nextRun - now;
                    _logger.LogInformation($"Next story generation scheduled for: {nextRun:yyyy-MM-dd HH:mm:ss} UTC (in {delay.TotalHours:F1} hours)");

                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await GenerateStoriesAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Story generation background service cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in story generation background service");
                    // Wait 1 hour before retrying on error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private async Task GenerateStoriesAsync()
        {
            try
            {
                _logger.LogInformation("Starting daily story generation");
                
                using var scope = _serviceProvider.CreateScope();
                var storyService = scope.ServiceProvider.GetRequiredService<IStoryService>();
                
                await storyService.GenerateStoriesForEligibleUsersAsync();
                
                _logger.LogInformation("Daily story generation completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during daily story generation");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Story Generation Background Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}