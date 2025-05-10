using System.Text;
using System.Text.Json;
using LLM.Configuration;
using LLM.Models;
using System.Net.Http.Headers;
using Api.Framework.Options; // Added for AuthenticationHeaderValue

namespace LLM.Services
{
    /// <summary>
    /// Service for recognizing the language of a given word or phrase using OpenRouter's API.
    /// </summary>
    public class LanguageRecognitionService
    {
        private readonly HttpClient _httpClient;
        private readonly LlmConfigurationService _configService;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageRecognitionService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making API requests.</param>
        /// <param name="configService">The configuration service for accessing API settings.</param>
        public LanguageRecognitionService(HttpClient httpClient, LlmConfigurationService configService)
        {
            _httpClient = httpClient;
            _configService = configService;
        }

        /// <summary>
        /// Recognizes the language of the input text and returns possible languages with confidence scores.
        /// </summary>
        /// <param name="inputText">The text to analyze for language recognition.</param>
        /// <returns>A <see cref="LanguageRecognitionResult"/> containing the input text and a list of possible languages with scores.</returns>
        public async Task<LanguageRecognitionResult> RecognizeLanguageAsync(string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                throw new ArgumentException("Input text cannot be empty or null.", nameof(inputText));
            }

            var result = new LanguageRecognitionResult { InputText = inputText };
            var agentConfigs = _configService.GetAgentConfigs();

            if (agentConfigs == null || agentConfigs.Count == 0)
            {
                throw new Exception("No LLM providers are configured.");
            }

            // For simplicity, try the first agent and its models
            var agent = agentConfigs[0];
            Console.WriteLine($"Using agent {agent.ApiProvider} for language recognition of text '{inputText}'");

            foreach (var currentModel in agent.Models)
            {
                if (string.IsNullOrEmpty(currentModel)) continue;

                try
                {
                    string apiUrl = agent.ApiBaseUrl.EndsWith("/") ? agent.ApiBaseUrl + "chat/completions" : agent.ApiBaseUrl + "/chat/completions";
                    if (agent.ApiProvider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase) && !agent.ApiBaseUrl.Contains("/api/v1"))
                    {
                        apiUrl = agent.ApiBaseUrl.TrimEnd('/') + "/api/v1/chat/completions";
                    }

                    var response = await MakeApiRequestAsync(inputText, currentModel, agent.ApiKey, apiUrl);
                    response = response.Trim();
                    result.Languages = _ParseLanguageScores(response);
                    return result;
                }
                catch (Exception ex)
                {
                    // Log the error if needed
                    Console.WriteLine($"Error with model {currentModel} for agent {agent.ApiProvider}: {ex.Message}");
                    // Continue to the next model in this agent's list
                }
            }

            throw new Exception($"All models for agent {agent.ApiProvider} failed to respond. Please check API configuration or model availability.");
        }

        private async Task<string> MakeApiRequestAsync(string inputText, string model, string apiKey, string apiRequestUrl)
        {
            var prompt = $"Analyze the following text and identify the language(s) it might belong to with confidence scores (0 to 1). Return the result in a structured JSON format. Text: \"{inputText}\"";
            var sampleJson = @"{
                ""languages"": [
                    { ""language"": ""English"", ""score"": 0.99 },
                    { ""language"": ""Spanish"", ""score"": 0.3 }
                ]
            }";
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = $"You are a language detection expert. Always respond with plain JSON string without wrapping it in code blocks or any other formatting. Here's a sample response format:\n{sampleJson}" },
                    new { role = "user", content = prompt }
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiRequestUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private static List<LanguageScore> _ParseLanguageScores(string apiResponse)
        {
            var scores = new List<LanguageScore>();
            try
            {
                // Deserialize the full API response to extract the content field
                var responseObj = JsonSerializer.Deserialize<OpenRouterResponse>(apiResponse, JsonOptions.CaseInsensitive);
                if (responseObj?.Choices is {Count: > 0,})
                {
                    var content = responseObj.Choices[0].Message.Content;
                    // Try to deserialize the content as plain JSON
                    var languageResponse = JsonSerializer.Deserialize<LanguageResponse>(content, JsonOptions.CaseInsensitive);
                    if (languageResponse?.Languages != null)
                    {
                        scores.AddRange(languageResponse.Languages);
                    }
                    else
                    {
                        scores.Add(new LanguageScore { Language = "Unknown", Score = 0.0 });
                    }
                }
                else
                {
                    scores.Add(new LanguageScore { Language = "Unknown", Score = 0.0 });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing API response: {ex.Message}");
                scores.Add(new LanguageScore { Language = "Error", Score = 0.0 });
            }

            return scores;
        }

        private class OpenRouterResponse
        {
            public List<Choice> Choices { get; set; } = new();
        }

        private class Choice
        {
            public Message Message { get; set; } = new Message();
        }

        private class Message
        {
            public string Content { get; set; } = string.Empty;
        }

        private class LanguageResponse
        {
            public List<LanguageScore> Languages { get; set; } = new();
        }
    }
}
