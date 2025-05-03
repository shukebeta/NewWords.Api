using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LLM.Configuration;
using LLM.Models;
using System.Net.Http.Headers; // Added for AuthenticationHeaderValue

namespace LLM.Services
{
    /// <summary>
    /// Service for recognizing the language of a given word or phrase using OpenRouter's API.
    /// </summary>
    public class LanguageRecognitionService
    {
        private readonly HttpClient _httpClient;
        private readonly LLMConfigurationService _configService;
        private readonly string _apiUrl = "https://openrouter.ai/api/v1/chat/completions";

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageRecognitionService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making API requests.</param>
        /// <param name="configService">The configuration service for accessing API settings.</param>
        public LanguageRecognitionService(HttpClient httpClient, LLMConfigurationService configService)
        {
            _httpClient = httpClient;
            _configService = configService;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configService.ApiKey);
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
            string currentModel = _configService.GetPrimaryModel();

            while (!string.IsNullOrEmpty(currentModel))
            {
                try
                {
                    var response = await MakeApiRequestAsync(inputText, currentModel);
                    result.Languages = ParseLanguageScores(response);
                    return result;
                }
                catch (Exception ex)
                {
                    // Log the error if needed
                    Console.WriteLine($"Error with model {currentModel}: {ex.Message}");
                    currentModel = _configService.GetFallbackModel(currentModel);
                    if (!string.IsNullOrEmpty(currentModel))
                    {
                        Console.WriteLine($"Falling back to model: {currentModel}");
                    }
                }
            }

            throw new Exception("All configured models failed to respond. Please check API key or model availability.");
        }

        private async Task<string> MakeApiRequestAsync(string inputText, string model)
        {
            var prompt = $"Analyze the following text and identify the language(s) it might belong to with confidence scores (0 to 1). Text: \"{inputText}\"";
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are a language detection expert." },
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private List<LanguageScore> ParseLanguageScores(string apiResponse)
        {
            // This is a simplified parsing logic. In a real scenario, you would deserialize the JSON response
            // and extract the language scores based on the expected format from OpenRouter's API.
            // For now, we'll return dummy data as a placeholder.

            var scores = new List<LanguageScore>();
            try
            {
                // Placeholder parsing logic
                // In reality, you would use JsonSerializer.Deserialize to parse the response
                // and extract the content from the "choices" array.
                if (apiResponse.Contains("English"))
                {
                    scores.Add(new LanguageScore { Language = "English", Score = 0.95 });
                }
                if (apiResponse.Contains("Spanish"))
                {
                    scores.Add(new LanguageScore { Language = "Spanish", Score = 0.3 });
                }
                // Add more languages as needed based on response content
                if (scores.Count == 0)
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
    }
}