using LLM.Models;
using Microsoft.Extensions.Configuration;

namespace LLM.Services;

public class ConfigurationService(IConfiguration configuration) : IConfigurationService
{
    private readonly List<AgentConfig> _agentConfigs = configuration.GetSection("Agents").Get<List<AgentConfig>>() ?? [];
    private List<Agent>? _agents;
    private Dictionary<string, string>? _languageLookup;

    public List<Language> SupportedLanguages { get; } = configuration
        .GetSection("SupportedLanguages")
        .Get<List<Language>>() ?? [];

    public List<Agent> Agents => _agents ??= _agentConfigs
        .SelectMany(a => a.Models.Select(m => new Agent
        {
            Provider = a.Provider,
            ModelName = m,
            BaseUrl = a.BaseUrl,
            ApiKey = a.ApiKey
        }))
        .ToList();

    /// <summary>
    /// Gets the language name based on the provided language code.
    /// Optimized with dictionary lookup for better performance.
    /// </summary>
    /// <param name="code">The language code to look up.</param>
    /// <returns>The name of the language if found; otherwise, null.</returns>
    public string? GetLanguageName(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        _languageLookup ??= SupportedLanguages.ToDictionary(l => l.Code, l => l.Name);

        return _languageLookup.GetValueOrDefault(code);
    }


}