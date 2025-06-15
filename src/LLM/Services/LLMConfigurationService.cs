using LLM.Models;
using Microsoft.Extensions.Configuration;

namespace LLM.Services;



public class LlmConfigurationService : ILlmConfigurationService
{
    private readonly List<AgentConfig> _agentConfigs;
    private List<Agent>? _agents;

    public LlmConfigurationService(IConfiguration configuration)
    {
        _agentConfigs = configuration.GetSection("Agents").Get<List<AgentConfig>>() ?? [];
    }

    public List<Agent> Agents => _agents ??= _agentConfigs
        .SelectMany(a => a.Models.Select(m => new Agent
        {
            Provider = a.Provider,
            ModelName = m,
            BaseUrl = a.BaseUrl,
            ApiKey = a.ApiKey
        }))
        .ToList();

    public class AgentConfig
    {
        public string Provider { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public List<string> Models { get; set; } = [];
    }
}