using SqlSugar;

namespace NewWords.Api.Entities
{
    /// <summary>
    /// Represents the configuration for an available Large Language Model (LLM)
    /// accessible via a provider like OpenRouter.
    /// </summary>
    [SugarTable("LlmConfigurations")]
    [SugarIndex("UQ_ModelName", nameof(ModelName), OrderByType.Asc, true)] // Added length
    public class LlmConfiguration
    {
        /// <summary>
        /// Unique identifier for the LLM configuration (Primary Key, Auto-Increment).
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int LlmConfigId { get; set; }

        /// <summary>
        /// The unique model identifier used by the provider (e.g., "openai/gpt-3.5-turbo", "google/gemini-pro"). Required, Unique.
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 100)] // Added length
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// A user-friendly display name for the model (e.g., "GPT-3.5 Turbo", "Gemini Pro"). Optional.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 100)] // Added length
        public string? DisplayName { get; set; }

        /// <summary>
        /// Flag indicating whether this model configuration is currently active and can be used for generation. Required. Defaults to true.
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// API Key associated with this specific model or the provider (e.g., OpenRouter key).
        /// IMPORTANT: Storing keys directly here is NOT recommended for production.
        /// Consider using .NET Secret Manager, environment variables, or a dedicated secrets service.
        /// This field is included per the initial design but should be handled securely. Nullable.
        /// </summary>
        [SugarColumn(IsNullable = true, Length = 1024)]
        public string? ApiKey { get; set; } // Nullable, handle secure storage separately

        /// <summary>
        /// Timestamp when the configuration was created (Required, Unix timestamp as long).
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long CreatedAt { get; set; } = 0; // Default to 0 (Unix epoch start)
    }
}
