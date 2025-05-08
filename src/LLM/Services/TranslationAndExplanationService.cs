using System; // Added for Exception, ArgumentException
using System.Collections.Generic; // Added for List
using System.Net.Http; // Added for HttpClient
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks; // Added for Task, Task.Delay
using Api.Framework.Options; // Added for AuthenticationHeaderValue
using LLM.Configuration; // LlmConfigurationService, AgentConfig
using LLM.Models;

namespace LLM.Services
{
    /// <summary>
    /// Represents the result of an explanation attempt.
    /// </summary>
    public class ExplanationResult
    {
        public bool IsSuccess { get; set; }
        public string? Markdown { get; set; }
        public string? ModelName { get; set; }
        public int? HttpStatusCode { get; set; } // To store the status code on failure
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service for providing phonetic transcription, explanations, and translations
    /// of words or phrases using various LLM providers.
    /// </summary>
    public class TranslationAndExplanationService
    {
        private readonly HttpClient _httpClient;
        // private readonly LlmConfigurationService _configService; // No longer needed directly for API key/URL

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslationAndExplanationService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making API requests.</param>
        public TranslationAndExplanationService(HttpClient httpClient) // LlmConfigurationService removed
        {
            _httpClient = httpClient;
            // _configService = configService; // Removed
            // _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configService.ApiKey); // API key is now per-agent
        }

        /// <summary>
        /// Gets a Markdown-formatted explanation for the input text in the target language using a specific agent.
        /// </summary>
        /// <param name="inputText">The word or phrase to explain.</param>
        /// <param name="targetLanguage">The language for the explanation.</param>
        /// <param name="agent">The agent configuration to use for this request.</param>
        /// <returns>An <see cref="ExplanationResult"/> detailing success or failure.</returns>
        /// <exception cref="ArgumentException">Thrown if input text, target language, or agent is null, or agent details are invalid.</exception>
        public async Task<ExplanationResult> GetMarkdownExplanationAsync(string inputText, string targetLanguage, LlmConfigurationService.AgentConfig agent)
        {
            ValidateInput(inputText, targetLanguage);
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            if (string.IsNullOrEmpty(agent.ApiBaseUrl)) throw new ArgumentException("Agent ApiBaseUrl cannot be empty.", nameof(agent));
            if (string.IsNullOrEmpty(agent.ApiKey)) throw new ArgumentException("Agent ApiKey cannot be empty.", nameof(agent));
            if (agent.Models == null || !agent.Models.Any()) throw new ArgumentException("Agent Models list cannot be null or empty.", nameof(agent));

            const int maxRetries = 1; // Retries for the same agent and its models. Controller handles retrying different agents.
                                     // Let's simplify to 1 attempt per agent call from controller for now.
                                     // If we want retries for transient errors for the *same* agent, this can be > 1.
                                     // For now, the controller will iterate agents, so this service call is one attempt for *this* agent.

            const int delayBetweenRetriesMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                foreach (var currentModel in agent.Models)
                {
                    if (string.IsNullOrEmpty(currentModel)) continue; // Skip empty model names

                    try
                    {
                        // Construct the full API endpoint URL for chat completions if not already part of ApiBaseUrl
                        // Assuming ApiBaseUrl might be just the base, e.g., "https://openrouter.ai/api/v1"
                        // And the specific path is "/chat/completions"
                        // This might need adjustment based on how ApiBaseUrl is defined in appsettings
                        string apiUrl = agent.ApiBaseUrl.EndsWith("/") ? agent.ApiBaseUrl + "chat/completions" : agent.ApiBaseUrl + "/chat/completions";

                        var apiResponseJson = await _MakeMarkdownApiRequestAsync(inputText, targetLanguage, currentModel, agent.ApiKey, apiUrl);
                        var responseObj = JsonSerializer.Deserialize<OpenRouterResponse>(apiResponseJson, JsonOptions.CaseInsensitive);

                        if (responseObj?.Choices != null && responseObj.Choices.Count > 0 && !string.IsNullOrWhiteSpace(responseObj.Choices[0].Message.Content))
                        {
                            return new ExplanationResult
                            {
                                IsSuccess = true,
                                Markdown = responseObj.Choices[0].Message.Content.Trim(),
                                ModelName = currentModel
                            };
                        }
                        // Log non-critical failure for this model (e.g., empty content)
                        Console.WriteLine($"Attempt {attempt}/{maxRetries}: Model {currentModel} for agent {agent.ApiProvider} returned empty or unparsable content.");
                        // Continue to the next model in this agent's list
                    }
                    catch (HttpRequestException httpEx)
                    {
                        Console.WriteLine($"Attempt {attempt}/{maxRetries}: HTTP Error with model {currentModel} for agent {agent.ApiProvider}: {httpEx.Message}. Status: {httpEx.StatusCode}");
                        // For 4xx errors, we want to report this failure for the agent immediately.
                        if (httpEx.StatusCode.HasValue && (int)httpEx.StatusCode >= 400 && (int)httpEx.StatusCode < 500)
                        {
                            return new ExplanationResult
                            {
                                IsSuccess = false,
                                HttpStatusCode = (int)httpEx.StatusCode,
                                ErrorMessage = $"Agent {agent.ApiProvider} (Model: {currentModel}) failed with HTTP {(int)httpEx.StatusCode}: {httpEx.Message}",
                                ModelName = currentModel
                            };
                        }
                        // For other HTTP errors (5xx, network issues), log and continue to next model or retry if configured.
                        // If maxRetries > 1, the outer loop would handle retrying the agent.
                        // Since maxRetries is 1 for now, this means trying other models in the list.
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Attempt {attempt}/{maxRetries}: Error with model {currentModel} for agent {agent.ApiProvider}: {ex.Message}");
                        // Continue to the next model in this agent's list
                    }
                } // End foreach model in agent.Models

                // If all models for this agent failed in this attempt
                Console.WriteLine($"Attempt {attempt}/{maxRetries} failed for all models of agent {agent.ApiProvider}.");

                if (attempt < maxRetries)
                {
                    await Task.Delay(delayBetweenRetriesMs);
                    Console.WriteLine($"Retrying agent {agent.ApiProvider} ({attempt + 1}/{maxRetries})...");
                }
            } // End for loop (retries for this agent)

            return new ExplanationResult
            {
                IsSuccess = false,
                ErrorMessage = $"All models/attempts failed for agent {agent.ApiProvider} to provide a Markdown explanation for input: '{inputText}'."
            };
        }

        /// <summary>
        /// Gets structured linguistic information (IPA, translations, examples, etc.) for the input text.
        /// </summary>
        /// <param name="inputText">The word or phrase to analyze.</param>
        /// <param name="targetLanguage">The language for translations and analysis.</param>
        /// <returns>A <see cref="WordExplanationResult"/> containing the structured data.</returns>
        /// <exception cref="ArgumentException">Thrown if input text or target language is empty.</exception>
        /// <exception cref="Exception">Thrown if all configured models fail.</exception>
        public async Task<WordExplanationResult> GetStructuredExplanationAsync(string inputText, string targetLanguage)
        {
            ValidateInput(inputText, targetLanguage);
            // This method needs to be updated similarly to GetMarkdownExplanationAsync to accept AgentConfig
            // and iterate through its models, and return a more detailed result or throw specific exceptions.
            // For now, per the plan, focus is on GetMarkdownExplanationAsync.
            // Placeholder for future implementation:
            // if (_configService == null) throw new InvalidOperationException("_configService is null, GetStructuredExplanationAsync needs to be updated for multi-provider support.");

            // string currentModel = _configService.GetPrimaryModel(); // This uses the old single-provider config

            // while (!string.IsNullOrEmpty(currentModel))
            // {
            //     try
            //     {
            //         // This call needs to be updated to use AgentConfig for ApiKey and ApiBaseUrl
            //         var apiResponse = await _MakeStructuredApiRequestAsync(inputText, targetLanguage, currentModel, _configService.ApiKey, _configService.ApiProvider); // Placeholder: ApiProvider used as BaseUrl, needs fix
            //         var result = _ParseStructuredExplanationResponse(apiResponse, inputText);
            //         if (result.TextLanguage != "Unknown" || !string.IsNullOrEmpty(result.PrimaryTranslation))
            //         {
            //              return result;
            //         }
            //         throw new Exception($"Failed to parse structured JSON response from model {currentModel}.");
            //     }
            //     catch (Exception ex)
            //     {
            //         // This also needs to use agent-specific models, not _configService.GetFallbackModel
            //         LogAndSelectFallbackModel(ex, ref currentModel); // Removed _configService
            //     }
            // }
            // throw new Exception("All configured models failed to provide structured explanation data.");
            // Temporarily making this method non-functional until fully refactored for multi-provider
            Console.WriteLine("GetStructuredExplanationAsync is not yet refactored for multi-provider support and will throw.");
            throw new NotImplementedException("GetStructuredExplanationAsync is not yet refactored for multi-provider support.");
        }

        // --- Private Helper Methods ---

        private void ValidateInput(string inputText, string targetLanguage)
        {
             if (string.IsNullOrEmpty(inputText))
            {
                throw new ArgumentException("Input text cannot be empty or null.", nameof(inputText));
            }
            if (string.IsNullOrEmpty(targetLanguage))
            {
                throw new ArgumentException("Target language cannot be empty or null.", nameof(targetLanguage));
            }
        }

        // Obsolete or needs rework for agent-based model iteration
        private void LogAndSelectFallbackModel(Exception ex, ref string currentModel /*, LlmConfigurationService configService */)
        {
            Console.WriteLine($"Error with model {currentModel}: {ex.Message}");
            // currentModel = configService.GetFallbackModel(currentModel); // This logic is now per-agent
            // For now, this method is problematic with the new AgentConfig structure.
            // The calling method should handle iterating agent.Models.
            currentModel = string.Empty; // Stop trying models for this agent on error for now
            Console.WriteLine($"Falling back logic needs rework for agent-based models. Stopping model attempts for this agent.");
        }

        private async Task<string> _MakeMarkdownApiRequestAsync(string inputText, string targetLanguage, string model, string apiKey, string apiRequestUrl)
        {
            var userPrompt = $@"You are a language learning assistant that provides detailed explanations when given a word or phrase. When I provide a word or phrase in any language and specify my target Language ""{targetLanguage}"", please respond with:

1. The word/phrase with its IPA Transcription
2. Any relevant grammatical information (tense, part of speech, etc.) in {targetLanguage}
3. A clear definition
4. 2-3 example sentences using the word/phrase in original language with translations
5. 3-4 related words/synonyms/antonyms with pronunciations and translations and one example sentence

If I provide a complete sentence instead of a single word or phrase, please:
1. Provide a clear translation of the full sentence in the target language
2. Break down the sentence structure and explain its grammatical components
3. Identify and explain any idioms, colloquialisms, or potentially difficult parts of the sentence
4. Provide alternative ways to express the same meaning

Important: Do not label translations with ""{targetLanguage}:"" before each translated sentence. Simply provide the translation directly after the original sentence.

Format your response in a clear, structured way with bold headings and good spacing to make it easy to read. 
**IMPORTANT:**
RESPOND WITH THE MARKDOWN CONTENT ONLY.
DO NOT INCLUDE ANY INTRODUCTORY TEXT, CONCLUDING REMARKS, OR CODE FENCES (like ```markdown)'

My request is: {inputText}
";

            var systemPrompt = "You are a linguistic expert generating helpful, concise Markdown explanations for language learners. Respond ONLY with the requested Markdown text, nothing else.";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3f
            };

            return await _SendApiRequestAsync(requestBody, apiKey, apiRequestUrl);
        }

        private async Task<string> _MakeStructuredApiRequestAsync(string inputText, string targetLanguage, string model, string apiKey, string apiBaseUrl)
        {
            // This method also needs to use the passed apiKey and a correctly constructed apiRequestUrl from apiBaseUrl
            string apiRequestUrl = apiBaseUrl.EndsWith("/") ? apiBaseUrl + "chat/completions" : apiBaseUrl + "/chat/completions";
             if (apiBaseUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase) && !apiBaseUrl.Contains("/api/v1"))
            {
                apiRequestUrl = apiBaseUrl.TrimEnd('/') + "/api/v1/chat/completions";
            }


            var userPrompt = $"Generate a detailed linguistic analysis JSON object for the input text: \"{inputText}\". Provide the analysis in {targetLanguage}. Include ONLY the following components: 1. Recognized language of the input text ('textLanguage'). 2. IPA transcription ('ipaTranscription'). 3. Part of speech ('partOfSpeech'). 4. Primary translation ('primaryTranslation'). 5. Alternative translations ('alternativeTranslations', array). 6. Detailed explanation ('detailedExplanation'). 7. Example sentences with translations ('exampleSentences', array of objects with 'original' and 'translation'). 8. Related vocabulary terms ('relatedTerms', array of objects with 'term', 'ipaTranscription', 'partOfSpeech', 'meaning'). ";

            var sampleJson = @"{
                ""inputText"": ""bargain hunters"",
                ""textLanguage"": ""English"",
                ""ipaTranscription"": ""[ˈbɑːrɡən ˌhʌntərz]"",
                ""partOfSpeech"": ""n."",
                ""primaryTranslation"": ""捡便宜的人"",
                ""alternativeTranslations"": [""淘便宜货的人""],
                ""detailedExplanation"": ""Description here..."",
                ""exampleSentences"": [ { ""original"": ""Sentence 1"", ""translation"": ""Translation 1"" } ],
                ""relatedTerms"": [ { ""term"": ""term1"", ""ipaTranscription"": ""ipa1"", ""partOfSpeech"": ""pos1"", ""meaning"": ""meaning1"" } ]
            }";

            var systemPrompt = $@"You are a linguistic expert specializing in detailed word/phrase analysis and translation.
Your task is to generate ONLY a valid JSON object containing the linguistic analysis requested in the user prompt.
**Response Format Constraints:** (omitted for brevity, same as before)
**Example JSON Structure:** (omitted for brevity)
Remember: Your entire response must be ONLY the JSON object, starting with `{{` and ending with `}}`." ;


            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3f
            };

             return await _SendApiRequestAsync(requestBody, apiKey, apiRequestUrl);
        }

         private async Task<string> _SendApiRequestAsync(object requestBody, string apiKey, string apiRequestUrl)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, apiRequestUrl);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);

            // We will check for success status code in the calling method to get more details
            // response.EnsureSuccessStatusCode(); // Throws if status code is not 2xx. Moved to allow status code access.

            if (!response.IsSuccessStatusCode)
            {
                // Throw an HttpRequestException that includes the status code
                // This allows the caller to inspect it.
                var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Content: {errorContent}", null, response.StatusCode);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent.Trim();
        }


        private WordExplanationResult _ParseStructuredExplanationResponse(string apiResponse, string inputText)
        {
            var result = new WordExplanationResult { InputText = inputText, TextLanguage = "Unknown" }; // Default to Unknown
            try
            {
                // Deserialize the full API response to extract the content field
                var responseObj = JsonSerializer.Deserialize<OpenRouterResponse>(apiResponse, JsonOptions.CaseInsensitive);
                if (responseObj?.Choices != null && responseObj.Choices.Count > 0)
                {
                    var content = responseObj.Choices[0].Message.Content;

                    // Clean the content string: Trim whitespace and remove potential JSON fences
                    var cleanedContent = content.Trim();
                    if (cleanedContent.StartsWith("```json"))
                    {
                        cleanedContent = cleanedContent.Substring(7); // Remove ```json
                    }
                    if (cleanedContent.StartsWith("```"))
                    {
                         cleanedContent = cleanedContent.Substring(3); // Remove ```
                    }
                    if (cleanedContent.EndsWith("```"))
                    {
                        cleanedContent = cleanedContent.Substring(0, cleanedContent.Length - 3); // Remove ```
                    }
                    cleanedContent = cleanedContent.Trim(); // Trim again after removing fences

                    WordExplanationResult? explanationResult = null;
                    try
                    {
                        // First attempt: Try deserializing the cleaned content directly
                        explanationResult = JsonSerializer.Deserialize<WordExplanationResult>(cleanedContent, JsonOptions.CaseInsensitive);
                    }
                    catch (JsonException jsonEx) when (jsonEx.Message.Contains("'0x0A' is invalid within a JSON string"))
                    {
                        Console.WriteLine($"Initial JSON parsing failed due to unescaped newline. Attempting fix... Original message: {jsonEx.Message}");
                        // Second attempt: If the specific newline error occurred, try replacing \n with \\n and parse again
                        try
                        {
                            var potentiallyFixedContent = cleanedContent.Replace("\n", "\\n");
                            explanationResult = JsonSerializer.Deserialize<WordExplanationResult>(potentiallyFixedContent, JsonOptions.CaseInsensitive);
                            Console.WriteLine("JSON parsing succeeded after fixing newlines.");
                        }
                        catch (Exception ex)
                        {
                            // Log the error from the second attempt
                            Console.WriteLine($"Error parsing API response even after attempting newline fix: {ex.Message}");
                            // Keep explanationResult as null to fall through to error handling below
                        }
                    }
                    catch (Exception ex)
                    {
                         // Log other initial parsing errors
                         Console.WriteLine($"Error parsing API response: {ex.Message}");
                         // Keep explanationResult as null
                    }


                    if (explanationResult != null)
                    {
                        // Deserialization successful (either first or second attempt)
                        result.TextLanguage = string.IsNullOrEmpty(explanationResult.TextLanguage) ? "Unknown" : explanationResult.TextLanguage;
                        result.IpaTranscription = explanationResult.IpaTranscription;
                        result.PartOfSpeech = explanationResult.PartOfSpeech;
                        result.PrimaryTranslation = explanationResult.PrimaryTranslation;
                        result.AlternativeTranslations = explanationResult.AlternativeTranslations;
                        result.DetailedExplanation = explanationResult.DetailedExplanation;
                        result.ExampleSentences = explanationResult.ExampleSentences;
                        result.RelatedTerms = explanationResult.RelatedTerms;
                        return result; // Successfully parsed
                    }
                }
                 // Fallback if JSON structure not found or deserialization fails after attempts
                Console.WriteLine("Unable to parse structured response from API content.");
                result.DetailedExplanation = "Unable to parse structured response from API."; // Keep TextLanguage as Unknown
            }
            catch (Exception ex) // Catch errors from deserializing the *outer* OpenRouterResponse
            {
                Console.WriteLine($"Error parsing outer API response structure: {ex.Message}");
                result.DetailedExplanation = "Error parsing API response structure."; // Keep TextLanguage as Unknown
            }

            return result; // Return result with error state/defaults
        }

        // --- Private Helper Classes for Outer API Response Deserialization ---
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
