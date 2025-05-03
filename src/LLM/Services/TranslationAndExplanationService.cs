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
    /// Service for providing phonetic transcription and explanations of words or phrases in a target language using OpenRouter's API.
    /// </summary>
    public class TranslationAndExplanationService
    {
        private readonly HttpClient _httpClient;
        private readonly LLMConfigurationService _configService;
        private readonly string _apiUrl = "https://openrouter.ai/api/v1/chat/completions";

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslationAndExplanationService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making API requests.</param>
        /// <param name="configService">The configuration service for accessing API settings.</param>
        public TranslationAndExplanationService(HttpClient httpClient, LLMConfigurationService configService)
        {
            _httpClient = httpClient;
            _configService = configService;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configService.ApiKey);
        }

        /// <summary>
        /// Provides a detailed explanation of the input text, including phonetic transcription and translation in the target language.
        /// </summary>
        /// <param name="inputText">The word or phrase to explain.</param>
        /// <param name="targetLanguage">The language in which to provide the explanation and translation.</param>
        /// <returns>A <see cref="WordExplanationResult"/> containing the detailed explanation and related information.</returns>
        public async Task<WordExplanationResult> ExplainWordAsync(string inputText, string targetLanguage)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                throw new ArgumentException("Input text cannot be empty or null.", nameof(inputText));
            }
            if (string.IsNullOrEmpty(targetLanguage))
            {
                throw new ArgumentException("Target language cannot be empty or null.", nameof(targetLanguage));
            }

            var result = new WordExplanationResult { InputText = inputText };
            string currentModel = _configService.GetPrimaryModel();

            while (!string.IsNullOrEmpty(currentModel))
            {
                try
                {
                    var response = await MakeApiRequestAsync(inputText, targetLanguage, currentModel);
                    result = ParseExplanationResponse(response, inputText);
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

        private async Task<string> MakeApiRequestAsync(string inputText, string targetLanguage, string model)
        {
            var prompt = $"Provide a detailed explanation for the word or phrase \"{inputText}\" in {targetLanguage}. Include the International Phonetic Alphabet (IPA) transcription, part of speech, primary meaning, alternative meanings if any, a detailed explanation, example sentences with translations, and related vocabulary with their meanings. Return the response in a structured JSON format.";
            var sampleJson = @"{
                ""inputText"": ""bargain hunters"",
                ""ipaTranscription"": ""[ˈbɑːrɡən ˌhʌntərz]"",
                ""partOfSpeech"": ""n."",
                ""primaryTranslation"": ""捡便宜的人"",
                ""alternativeTranslations"": [""淘便宜货的人""],
                ""detailedExplanation"": ""“bargain hunters” 指的是那些专门寻找打折商品、促销优惠或低价好货的人。他们购物时非常关注价格，喜欢比价，通常会等待打折季或在跳蚤市场、电商促销中淘到性价比高的商品。"",
                ""exampleSentences"": [
                    {
                        ""original"": ""Bargain hunters rushed to the mall for Black Friday deals."",
                        ""translation"": ""淘便宜的人们蜂拥至商场抢购黑色星期五的促销商品。""
                    },
                    {
                        ""original"": ""The store was full of bargain hunters looking for end-of-season discounts."",
                        ""translation"": ""商店里挤满了寻找季末折扣的捡便宜者。""
                    }
                ],
                ""relatedTerms"": [
                    {
                        ""term"": ""bargain"",
                        ""ipaTranscription"": ""[ˈbɑːrɡən]"",
                        ""partOfSpeech"": ""n."",
                        ""meaning"": ""便宜货，划算的交易""
                    },
                    {
                        ""term"": ""discount"",
                        ""ipaTranscription"": ""[ˈdɪskaʊnt]"",
                        ""partOfSpeech"": ""n."",
                        ""meaning"": ""折扣""
                    }
                ]
            }";
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = $"You are a linguistic expert specializing in detailed word explanations and translations. Always respond with plain JSON string without wrapping it in code blocks or any other formatting. Here's a sample response format:\n{sampleJson}" },
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private WordExplanationResult ParseExplanationResponse(string apiResponse, string inputText)
        {
            var result = new WordExplanationResult { InputText = inputText };
            try
            {
                // Deserialize the full API response to extract the content field
                var responseObj = JsonSerializer.Deserialize<OpenRouterResponse>(apiResponse);
                if (responseObj?.Choices != null && responseObj.Choices.Count > 0)
                {
                    var content = responseObj.Choices[0].Message.Content;
                    // Try to deserialize the content as plain JSON
                    var explanationResult = JsonSerializer.Deserialize<WordExplanationResult>(content);
                    if (explanationResult != null)
                    {
                        return explanationResult;
                    }
                }
                // Fallback if JSON structure not found or deserialization fails
                result.DetailedExplanation = "Unable to parse structured response from API.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing API response: {ex.Message}");
                result.DetailedExplanation = "Error parsing response from API.";
            }

            return result;
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
    }
}