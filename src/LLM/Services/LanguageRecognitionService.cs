using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
                    response = response.Trim();
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

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private List<LanguageScore> ParseLanguageScores(string apiResponse)
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
            public List<Choice> Choices { get; set; } = new List<Choice>();
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
            public List<LanguageScore> Languages { get; set; } = new List<LanguageScore>();
        }
    }
}
