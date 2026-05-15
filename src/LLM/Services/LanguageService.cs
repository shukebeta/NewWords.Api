using System.Diagnostics;
using System.Text.Json;
using Api.Framework.Options;
using Flurl.Http;
using LLM.Models;
using Microsoft.Extensions.Logging;

namespace LLM.Services;

/// <summary>
/// Language Service, fully utilizing Flurl features
/// </summary>
public class LanguageService(IConfigurationService configurationService, ILogger<LanguageService> logger) : ILanguageService
{
    private const int ExplanationTimeoutSeconds = 12;
    private const int MaxExplanationFallbacks = 1;

    internal static string BuildExplanationSystemPrompt(string nativeLanguageName, string learningLanguageName)
    {
        return $$"""
                 You are a multilingual language tutor with strong real-world cultural and usage knowledge.
                 The user is a native {{nativeLanguageName}} speaker learning {{learningLanguageName}}.
                 They may know dictionary meanings, but they need practical usage, nuance, and examples.

                 Critical rules:
                 - Write the whole response in {{nativeLanguageName}} except for {{learningLanguageName}} words, phrases, IPA, and example sentences.
                 - Preserve the user's intent. If the input is a phrase, idiom, or comparison like "A vs B", explain the whole phrase or comparison instead of reducing it to one word.
                 - Only silently correct the input when it is clearly a typo of a single {{learningLanguageName}} word. Never mention the typo.
                 - If the input is not in {{learningLanguageName}}, say what it is usually called in {{learningLanguageName}} and explain the main usage differences if there are several options.
                 - The first non-empty line must be only the canonical word or phrase in bold, for example **apple** or **fathom vs understand**. Put nothing else on that line.
                 - No code blocks, no XML, no introductory filler, and no closing questions.

                 Required output:
                 - Use short markdown sections with paragraph breaks.
                 - Include IPA when helpful.
                 - Split clearly different meanings or senses.
                 - Include a clear meaning/explanation section in {{nativeLanguageName}}.
                 - Include an example sentences section for the queried word, phrase, or comparison itself. Provide 2 to 4 natural {{learningLanguageName}} examples, each followed by a concise {{nativeLanguageName}} translation or explanation.
                 - Include a related words or phrases section with 3 to 5 relevant {{learningLanguageName}} items.
                 - For each related item, include a short {{nativeLanguageName}} usage note; when possible, also include a very short {{learningLanguageName}} example sentence or usage snippet.
                 - Add cultural context only when it materially helps understanding.

                 Quality bar:
                 - Prefer practical nuance, register, collocation, and common usage over abstract dictionary wording.
                 - If there are several close alternatives, explain when to use each one.
                 - Do not omit the example sentences section or the related words section unless the input makes that impossible.

                 Examples:
                 - Input: fathom vs understand -> first line: **fathom vs understand**
                 - Input: ptofess -> first line: **profess**
                 """;
    }

    internal static IReadOnlyList<Agent> SelectExplanationAgents(
        IEnumerable<Agent> agents,
        IEnumerable<string> preferredExplanationModels)
    {
        var allAgents = agents.ToList();
        var agentsByModel = allAgents.ToLookup(agent => agent.ModelName, StringComparer.OrdinalIgnoreCase);
        var selectedAgents = new List<Agent>();

        foreach (var modelName in preferredExplanationModels)
        {
            var matchingAgent = agentsByModel[modelName].FirstOrDefault();
            if (matchingAgent != null)
            {
                selectedAgents.Add(matchingAgent);
            }
        }

        foreach (var agent in allAgents)
        {
            if (!selectedAgents.Contains(agent))
            {
                selectedAgents.Add(agent);
            }
        }

        return selectedAgents;
    }

    private static double GetElapsedMilliseconds(Stopwatch stopwatch)
    {
        return Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1);
    }

    /// <summary>
    /// Get Markdown formatted explanation using Flurl features
    /// </summary>
    /// <param name="inputText">Input text</param>
    /// <param name="nativeLanguageName"></param>
    /// <param name="learningLanguageName">Learning language</param>
    /// <param name="agent"></param>
    /// <returns>Explanation result</returns>
    private async Task<ExplanationResult> GetMarkdownExplanationAsync(
        string inputText,
        string nativeLanguageName,
        string learningLanguageName,
        Agent agent,
        int fallbackIndex)
    {
        var methodStopwatch = Stopwatch.StartNew();
        var providerHttpMs = 0d;
        var responseParseMs = 0d;
        var httpStatusCode = default(int?);
        var success = false;
        var errorMessage = string.Empty;
        var modelName = $"{agent.Provider}:{agent.ModelName}";
        var userPrompt = $"My input is: {inputText}";
        var systemPrompt = BuildExplanationSystemPrompt(nativeLanguageName, learningLanguageName);

        // Validate input
        if (string.IsNullOrEmpty(inputText))
            throw new ArgumentException("Input text cannot be empty", nameof(inputText));
        if (string.IsNullOrEmpty(learningLanguageName))
            throw new ArgumentException("Learning language cannot be empty", nameof(learningLanguageName));
        if (string.IsNullOrEmpty(agent.BaseUrl) || string.IsNullOrEmpty(agent.ApiKey) || string.IsNullOrEmpty(agent.ModelName))
            throw new ArgumentException("At least one of the Agent data is null", nameof(agent));

        try
        {
            var apiUrl = agent.BaseUrl.TrimEnd('/') + "/chat/completions";
            var requestData = new Dictionary<string, object?>
            {
                ["model"] = agent.ModelName,
                ["messages"] = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                ["temperature"] = 0.1f
            };

            if (string.Equals(agent.Provider, "OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                requestData["reasoning"] = new
                {
                    effort = "none",
                    exclude = true
                };
            }

            var providerStopwatch = Stopwatch.StartNew();

            var response = await apiUrl
                .WithHeader("Authorization", $"Bearer {agent.ApiKey}")
                .WithTimeout(TimeSpan.FromSeconds(ExplanationTimeoutSeconds))
                .AllowHttpStatus("4xx,5xx")
                .PostJsonAsync(requestData);

            providerStopwatch.Stop();
            providerHttpMs = GetElapsedMilliseconds(providerStopwatch);
            httpStatusCode = (int)response.ResponseMessage.StatusCode;

            if (!response.ResponseMessage.IsSuccessStatusCode)
            {
                var errorContent = await response.GetStringAsync();
                errorMessage = $"HTTP {response.ResponseMessage.StatusCode}: {errorContent}";
                logger.LogWarning("API request failed for word '{InputText}' with {ModelName}: {ErrorMessage}",
                    inputText, modelName, errorMessage);

                return new ExplanationResult
                {
                    IsSuccess = false,
                    ModelName = modelName,
                    HttpStatusCode = httpStatusCode,
                    ErrorMessage = errorMessage
                };
            }

            var parseStopwatch = Stopwatch.StartNew();
            var apiResponse = await response.GetJsonAsync<ApiCompletionResponse>();
            parseStopwatch.Stop();
            responseParseMs = GetElapsedMilliseconds(parseStopwatch);

            if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
            {
                var fullResponse = JsonSerializer.Serialize(apiResponse);
                errorMessage = $"API response contains no choices. Full response: {fullResponse}";
                return new ExplanationResult
                {
                    IsSuccess = false,
                    ModelName = modelName,
                    ErrorMessage = errorMessage
                };
            }

            var firstChoice = apiResponse.Choices[0];
            if (firstChoice?.Message?.Content == null || string.IsNullOrWhiteSpace(firstChoice.Message.Content))
            {
                var fullResponse = JsonSerializer.Serialize(apiResponse);
                errorMessage = $"API response choice contains no valid content. Full response: {fullResponse}";
                return new ExplanationResult
                {
                    IsSuccess = false,
                    ModelName = modelName,
                    ErrorMessage = errorMessage
                };
            }

            success = true;

            return new ExplanationResult
            {
                IsSuccess = true,
                Markdown = firstChoice.Message.Content.Trim(),
                ModelName = modelName,
            };
        }
        catch (Exception ex)
        {
            errorMessage = $"An exception occurred: {ex.Message}";
            logger.LogError(ex, "GetMarkdownExplanationAsync failed for word '{InputText}' with {ModelName}",
                inputText, modelName);

            return new ExplanationResult
            {
                IsSuccess = false,
                ModelName = modelName,
                ErrorMessage = errorMessage
            };
        }
        finally
        {
            methodStopwatch.Stop();
            logger.LogInformation(
                "GetMarkdownExplanationAsync timing for word '{InputText}': model_name={ModelName}, fallback_index={FallbackIndex}, success={Success}, provider_http_ms={ProviderHttpMs}, response_parse_ms={ResponseParseMs}, total_ms={TotalMs}, system_prompt_chars={SystemPromptChars}, user_prompt_chars={UserPromptChars}, http_status={HttpStatusCode}",
                inputText,
                modelName,
                fallbackIndex,
                success,
                providerHttpMs,
                responseParseMs,
                GetElapsedMilliseconds(methodStopwatch),
                systemPrompt.Length,
                userPrompt.Length,
                httpStatusCode);

            if (!success && !string.IsNullOrWhiteSpace(errorMessage))
            {
                logger.LogWarning("Explanation request did not succeed for word '{InputText}': model_name={ModelName}, fallback_index={FallbackIndex}, error={ErrorMessage}",
                    inputText, modelName, fallbackIndex, errorMessage);
            }
        }
    }

    /// <summary>
    /// Attempt to get explanation using multiple models, trying in sequence until successful
    /// </summary>
    public async Task<ExplanationResult> GetMarkdownExplanationWithFallbackAsync(
        string inputText,
        string nativeLanguage,
        string targetLanguage)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var explanationAgents = SelectExplanationAgents(configurationService.Agents, configurationService.PreferredExplanationModels);
        var attemptLimit = Math.Min(explanationAgents.Count, MaxExplanationFallbacks + 1);
        var attempts = 0;
        var lastModelName = string.Empty;
        var lastFallbackIndex = -1;
        var result = new ExplanationResult
        {
            IsSuccess = false,
            ErrorMessage = "No explanation models are configured."
        };

        if (attemptLimit == 0)
        {
            overallStopwatch.Stop();
            logger.LogError("GetMarkdownExplanationWithFallbackAsync cannot run for word '{InputText}' because no explanation models are configured",
                inputText);
            logger.LogInformation(
                "GetMarkdownExplanationWithFallbackAsync timing for word '{InputText}': success={Success}, attempts={Attempts}, total_ms={TotalMs}, model_name={ModelName}, fallback_index={FallbackIndex}",
                inputText,
                false,
                attempts,
                GetElapsedMilliseconds(overallStopwatch),
                lastModelName,
                lastFallbackIndex);
            return result;
        }

        for (var agentIndex = 0; agentIndex < attemptLimit; agentIndex++)
        {
            var agent = explanationAgents[agentIndex];
            attempts++;
            lastModelName = $"{agent.Provider}:{agent.ModelName}";
            lastFallbackIndex = agentIndex;

            result = await GetMarkdownExplanationAsync(inputText, nativeLanguage, targetLanguage, agent, agentIndex);

            if (result.IsSuccess)
            {
                overallStopwatch.Stop();
                logger.LogInformation(
                    "GetMarkdownExplanationWithFallbackAsync timing for word '{InputText}': success={Success}, attempts={Attempts}, total_ms={TotalMs}, model_name={ModelName}, fallback_index={FallbackIndex}",
                    inputText,
                    true,
                    attempts,
                    GetElapsedMilliseconds(overallStopwatch),
                    result.ModelName,
                    agentIndex);
                return result;
            }
        }

        overallStopwatch.Stop();
        logger.LogError("All preferred explanation models failed for word '{InputText}'. Last error: {ErrorMessage}",
            inputText, result.ErrorMessage);
        logger.LogInformation(
            "GetMarkdownExplanationWithFallbackAsync timing for word '{InputText}': success={Success}, attempts={Attempts}, total_ms={TotalMs}, model_name={ModelName}, fallback_index={FallbackIndex}",
            inputText,
            false,
            attempts,
            GetElapsedMilliseconds(overallStopwatch),
            lastModelName,
            lastFallbackIndex);

        return result;
    }

    /// <summary>
    /// Attempt to generate story using multiple models, trying in sequence until successful
    /// </summary>
    public async Task<StoryResult> GetStoryWithFallbackAsync(string words, string languageName, string nativeLanguageName)
    {
        StoryResult result = new StoryResult();
        foreach (var agent in configurationService.Agents)
        {
            result = await GetStoryAsync(words, languageName, nativeLanguageName, agent);
            if (result.IsSuccess)
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Generate story using a specific agent
    /// </summary>
    /// <param name="words">Comma-separated list of vocabulary words</param>
    /// <param name="languageName">Language to write the story in</param>
    /// <param name="nativeLanguageName">User's native language for explanations</param>
    /// <param name="agent">Agent configuration</param>
    /// <returns>Story generation result</returns>
    private async Task<StoryResult> GetStoryAsync(string words, string languageName, string nativeLanguageName, Agent agent)
    {
        // Validate input
        if (string.IsNullOrEmpty(words))
            throw new ArgumentException("Words cannot be empty", nameof(words));
        if (string.IsNullOrEmpty(languageName))
            throw new ArgumentException("Language name cannot be empty", nameof(languageName));
        if (string.IsNullOrEmpty(nativeLanguageName))
            throw new ArgumentException("Native language name cannot be empty", nameof(nativeLanguageName));
        if (string.IsNullOrEmpty(agent.BaseUrl) || string.IsNullOrEmpty(agent.ApiKey) || string.IsNullOrEmpty(agent.ModelName))
            throw new ArgumentException("At least one of the Agent data is null", nameof(agent));

        try
        {
            // Build the complete API endpoint
            var apiUrl = agent.BaseUrl.TrimEnd('/') + "/chat/completions";

            // Build the story generation prompt
            var userPrompt = $"Write a story using these words: {words}";
            var systemPrompt = $"""
                                You are a story generator that helps language learners remember vocabulary.

                                USER'S NATIVE LANGUAGE: {nativeLanguageName}
                                TARGET STORY LANGUAGE: {languageName}

                                CRITICAL FORMATTING RULES:
                                1. User target words: MUST use __word__ (explanation in {nativeLanguageName})
                                2. Complex words YOU add: MUST use **word** (explanation in {nativeLanguageName})
                                3. NEVER mix these formats.
                                4. ALL explanations MUST be in the user's native language: {nativeLanguageName}

                                PHRASE HANDLING RULES:
                                1. Multi-word phrases (like "go off", "look up") are SINGLE UNITS - never split them
                                2. Highlight ALL inflected forms: if user gives "go off", also highlight "went off", "going off", "goes off"
                                3. Apply same logic to all phrasal verbs and multi-word expressions
                                4. Each variation needs proper formatting and explanation

                                CROSS-LANGUAGE WORD HANDLING:
                                5. If user provides words in {nativeLanguageName} or other non-{languageName} languages, ALWAYS translate them to {languageName} equivalents first
                                6. NEVER insert non-{languageName} words directly into the {languageName} story text
                                7. Format: __[{languageName}_word]__ ([original_word])
                                8. Example: If user gives "梳理", write "The team decided to __organize__ (梳理) their thoughts"
                                9. WRONG: "The team decided to 梳理 (organize) their thoughts"
                                10. This maintains {languageName} story flow while teaching proper vocabulary

                                EXAMPLE: If user gives "negotiate, deadline, go off":
                                "The __deadline__ (截止日期) was approaching, so Maya had to __negotiate__ (协商) with her **supervisor** (主管). The alarm __went off__ (响起) at 6 AM."

                                REQUIREMENTS:
                                - Write 150-250 words in {languageName} (keep it concise and engaging)
                                - MANDATORY: Every underline word MUST have (explanation in {nativeLanguageName})
                                - MANDATORY: Every bold word MUST have (explanation in {nativeLanguageName})
                                - Add at least 3-5 complex words with bold formatting
                                - Output ONLY the story, no extra text

                                WRONG: "__Saturday__ Emily went hiking" or "The alarm __went__ __off__"
                                CORRECT: "__Saturday__ (星期六) Emily went **hiking** (远足)" or "The alarm __went off__ (响起)"
                                """;

            // Build the request body
            var requestData = new
            {
                model = agent.ModelName,
                messages = new[]
                {
                    new {role = "system", content = systemPrompt},
                    new {role = "user", content = userPrompt}
                },
                temperature = 0.7f, // Higher temperature for more creative stories
            };

            // Use Flurl's fluent API to send request with proper error handling
            var response = await apiUrl
                .WithHeader("Authorization", $"Bearer {agent.ApiKey}")
                .WithTimeout(TimeSpan.FromSeconds(30))
                .AllowHttpStatus("4xx,5xx")
                .PostJsonAsync(requestData);

            if (!response.ResponseMessage.IsSuccessStatusCode)
            {
                var errorContent = await response.GetStringAsync();
                return new StoryResult
                {
                    IsSuccess = false,
                    ModelName = agent.ModelName,
                    HttpStatusCode = (int)response.ResponseMessage.StatusCode,
                    ErrorMessage = $"HTTP {response.ResponseMessage.StatusCode}: {errorContent}"
                };
            }

            var apiResponse = await response.GetJsonAsync<ApiCompletionResponse>();

            // Parse the response with better validation
            if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
            {
                var fullResponse = JsonSerializer.Serialize(apiResponse);
                return new StoryResult
                {
                    IsSuccess = false,
                    ModelName = agent.ModelName,
                    ErrorMessage = $"API response contains no choices. Full response: {fullResponse}"
                };
            }

            var firstChoice = apiResponse.Choices[0];
            var content = firstChoice.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(content))
            {
                return new StoryResult
                {
                    IsSuccess = false,
                    ModelName = agent.ModelName,
                    ErrorMessage = "API returned empty content"
                };
            }

            return new StoryResult
            {
                IsSuccess = true,
                Content = content,
                ModelName = agent.ModelName
            };
        }
        catch (Exception ex)
        {
            return new StoryResult
            {
                IsSuccess = false,
                ModelName = agent.ModelName,
                ErrorMessage = $"Exception during API call: {ex.Message}"
            };
        }
    }

    public async Task<LanguageDetectionResult> GetDetectedLanguageWithFallbackAsync(string text)
    {
        LanguageDetectionResult result = new LanguageDetectionResult();
        foreach (var agent in configurationService.Agents)
        {
            result = await GetDetectedLanguageAsync(text, agent);
            if (result.IsSuccessful)
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Get markdown explanation using a specific agent (no fallback).
    /// Used when refreshing explanations to generate from a specific model.
    /// </summary>
    public async Task<string> GetMarkdownExplanationAsync(
        Agent agent,
        string wordText,
        string learningLanguageInEnglish,
        string explanationLanguageInEnglish)
    {
        var result = await GetMarkdownExplanationAsync(
            wordText,
            explanationLanguageInEnglish,
            learningLanguageInEnglish,
            agent,
            0);

        if (!result.IsSuccess)
        {
            throw new Exception($"Failed to generate explanation with {agent.Provider}:{agent.ModelName}: {result.ErrorMessage}");
        }

        return result.Markdown ?? string.Empty;
    }

    /// <summary>
    /// Detects the language of the given text and returns LanguageDetectionResult that contains the language code along with the confidence level.
    /// This method uses the existing LLM client for language detection.
    /// </summary>
    /// <param name="text">The input text to detect the language of.</param>
    /// <param name="agent">The Agent object containing configuration details.</param>
    /// <returns>A LanguageDetectionResult object containing the detected language and confidence.</returns>
    private async Task<LanguageDetectionResult> GetDetectedLanguageAsync(string text, Agent agent)
    {
        // Validate input
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Text cannot be empty", nameof(text));
        if (agent == null) throw new ArgumentException("Agent configuration cannot be null", nameof(agent));
        if (string.IsNullOrEmpty(agent.BaseUrl))
            throw new ArgumentException("Base URL cannot be empty", nameof(agent.BaseUrl));
        if (string.IsNullOrEmpty(agent.ApiKey))
            throw new ArgumentException("API key cannot be empty", nameof(agent.ApiKey));
        if (string.IsNullOrEmpty(agent.ModelName))
            throw new ArgumentException("Model name cannot be empty", nameof(agent.ModelName));

        // Build the prompt
        const string systemPrompt = """
                                    <language_detection>
                                        <role>
                                            <description>You are a language detection expert capable of identifying the language of given text snippets.</description>
                                        </role>
                                        <task>
                                            <action>Detect the language of the given text and provide a valid pure JSON string without any formatting, explanations or markdown.</action>
                                            <requirements>
                                                1. Return ONLY a valid JSON object with no additional formatting, explanations, or markdown
                                                2. For Chinese text detection:
                                                   - Use 'zh-CN' for 简体字
                                                   - Use 'zh-TW' for 繁体字
                                                3. Use standard ISO 639-1 codes for other languages (e.g., 'en', 'fr', 'es', 'de', 'ja', 'ko')
                                                4. Confidence should be a decimal between 0.0 and 1.0
                                                5. 请务必仔细检查是否繁体字，不要见了简体的"中华民国"也返回 zh-TW
                                            </requirements>
                                        </task>
                                        <exampleResponse>
                                            {"languageCode": "en", "confidenceLevel": 0.95}
                                        </exampleResponse>
                                    </language_detection>
                                    """;

        var userPrompt = $"input text is: {text}";

        // Build the request body
        var requestData = new
        {
            model = agent.ModelName,
            messages = new[]
            {
                new {role = "system", content = systemPrompt},
                new {role = "user", content = userPrompt}
            },
            temperature = 0.1f,
            max_tokens = 100,
        };

        // Use Flurl's fluent API to send request with proper error handling
        var apiUrl = agent.BaseUrl.TrimEnd('/') + "/chat/completions";
        var response = await apiUrl
            .WithHeader("Authorization", $"Bearer {agent.ApiKey}")
            .WithTimeout(TimeSpan.FromSeconds(15))
            .AllowHttpStatus("4xx,5xx")
            .PostJsonAsync(requestData);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var errorContent = await response.GetStringAsync();
            return new LanguageDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = $"HTTP {response.ResponseMessage.StatusCode}: {errorContent}"
            };
        }

        var apiResponse = await response.GetJsonAsync<ApiCompletionResponse>();

        // Parse the response with better validation
        if (apiResponse?.Choices == null || apiResponse.Choices.Length == 0)
        {
            var fullResponse = JsonSerializer.Serialize(apiResponse);
            return new LanguageDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = $"API response contains no choices. Full response: {fullResponse}"
            };
        }

        var firstChoice = apiResponse.Choices[0];
        if (firstChoice?.Message?.Content == null || string.IsNullOrWhiteSpace(firstChoice.Message.Content))
        {
            var fullResponse = JsonSerializer.Serialize(apiResponse);
            return new LanguageDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = $"API response choice contains no valid content. Full response: {fullResponse}"
            };
        }

        try
        {
            var responseContent = firstChoice.Message.Content.Trim();
            var jsonResponse = JsonSerializer.Deserialize<LanguageDetectionResult>(responseContent, JsonOptions.CaseInsensitive);

            if (jsonResponse != null)
            {
                jsonResponse.IsSuccessful = true;
                return jsonResponse;
            }

            return new LanguageDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = "Failed to deserialize language detection response"
            };
        }
        catch (JsonException jsonEx)
        {
            return new LanguageDetectionResult
            {
                IsSuccessful = false,
                ErrorMessage = $"Invalid JSON response: {jsonEx.Message}"
            };
        }
    }
}
