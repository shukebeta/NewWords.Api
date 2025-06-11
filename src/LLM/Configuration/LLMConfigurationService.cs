using Microsoft.Extensions.Configuration;

namespace LLM.Configuration
{
    /// <summary>
    /// Service for managing LLM configurations, including model lists and API keys.
    /// </summary>
    public class LlmConfigurationService
    {
        private readonly List<AgentConfig> _agentConfigs;

        /// <summary>
        /// Initializes a new instance of the <see cref="LlmConfigurationService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration instance to read settings from.</param>
        public LlmConfigurationService(IConfiguration configuration)
        {
            var configuration1 = configuration;
            _agentConfigs = configuration1.GetSection("Agents").Get<List<AgentConfig>>() ?? new List<AgentConfig>();
        }

        /// <summary>
        /// Gets the list of configured agents.
        /// </summary>
        /// <returns>A list of AgentConfig objects.</returns>
        public List<AgentConfig> GetAgentConfigs()
        {
            return _agentConfigs;
        }

        /// <summary>
        /// Represents the configuration structure for an agent in appsettings.json.
        /// </summary>
        public class AgentConfig // Made public to be accessible by TranslationAndExplanationService
        {
            public string Provider { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public List<string> Models { get; set; } = new();
        }
    }
}
