using Api.Framework;
using LLM;
using LLM.Services;
using NewWords.Api.Helpers;
using NewWords.Api.Options;
using NewWords.Api.Repositories;
using NewWords.Api.Services;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Extensions;

public static class ServiceExtensions
{
    public static void RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Configuration Options
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));

        // Register Application Services
        services.AddSingleton(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<IQueryHistoryService, QueryHistoryService>();
        services.AddSingleton<IUserWordRepository, UserWordRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IAccountService, AccountService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IVocabularyService, VocabularyService>();
        services.AddSingleton<IStoryService, StoryService>();
        
        // Register Background Services
        services.AddHostedService<StoryGenerationBackgroundService>();
        services.AddHostedService<ConfigurationSubscriberService>();
    }
}
