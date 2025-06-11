using LLM;
using LLM.Services;
using NewWords.Api.Helpers;
using NewWords.Api.Repositories;
using NewWords.Api.Services;
using NewWords.Api.Services.interfaces;

namespace NewWords.Api.Extensions;

public static class ServiceExtensions
{
    public static void RegisterServices(this IServiceCollection services)
    {
        // Register Application Services
        services.AddSingleton<LanguageHelper>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IVocabularyService, VocabularyService>();
        services.AddScoped<IQueryHistoryService, QueryHistoryService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserWordRepository, UserWordRepository>();
        services.AddScoped<ILlmConfigurationRepository, LlmConfigurationRepository>();
        services.AddScoped<LLM.Configuration.LlmConfigurationService>();
        services.AddScoped<ILanguageService, LanguageService>();
    }
}
