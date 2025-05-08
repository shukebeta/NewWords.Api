using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace LLM.Configuration
{
    /// <summary>
    /// Service for managing LLM configurations, including model lists and API keys.
    /// </summary>
    public class LlmConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly List<AgentConfig> _agentConfigs;

        /// <summary>
        /// Initializes a new instance of the <see cref="LlmConfigurationService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration instance to read settings from.</param>
        public LlmConfigurationService(IConfiguration configuration)
        {
            _configuration = configuration;
            _agentConfigs = _configuration.GetSection("Agents").Get<List<AgentConfig>>() ?? new List<AgentConfig>();
        }

        /// <summary>
        /// Gets the list of configured agents.
        /// </summary>
        /// <returns>A list of AgentConfig objects.</returns>
        public List<AgentConfig> GetAgentConfigs()
        {
            return _agentConfigs;
        }

        // Obsolete properties and methods related to a single provider configuration
        // /// <summary>
        // /// Gets the API provider name.
        // /// </summary>
        // public string ApiProvider => _apiProvider;
        //
        // /// <summary>
        // /// Gets the API key for the provider.
        // /// </summary>
        // public string ApiKey => _apiKey;
        //
        // /// <summary>
        // /// Gets the primary model to use for API calls.
        // /// </summary>
        // /// <returns>The primary model name if available; otherwise, an empty string.</returns>
        // public string GetPrimaryModel()
        // {
        //     return _models.FirstOrDefault() ?? string.Empty;
        // }
        //
        // /// <summary>
        // /// Gets the next available model as a fallback if the primary model fails.
        // /// </summary>
        // /// <param name="currentModel">The current model that failed.</param>
        // /// <returns>The next model in the list if available; otherwise, an empty string.</returns>
        // public string GetFallbackModel(string currentModel)
        // {
        //     var index = _models.IndexOf(currentModel);
        //     if (index < _models.Count - 1)
        //     {
        //         return _models[index + 1];
        //     }
        //     return GetPrimaryModel();
        // }

        /// <summary>
        /// Represents the configuration structure for an agent in appsettings.json.
        /// </summary>
        public class AgentConfig // Made public to be accessible by TranslationAndExplanationService
        {
            public string ApiProvider { get; set; } = string.Empty;
            public string ApiBaseUrl { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public List<string> Models { get; set; } = new List<string>();
        }
    }
}
