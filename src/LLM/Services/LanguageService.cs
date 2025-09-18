using System.Text.Json;
using LLM.Models;
using Flurl.Http;
using Api.Framework.Options;

namespace LLM.Services;

/// <summary>
/// Language Service, fully utilizing Flurl features
/// </summary>
public class LanguageService(IConfigurationService configurationService) : ILanguageService
{
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
        Agent agent)
    {
        // Validate input
        if (string.IsNullOrEmpty(inputText))
            throw new ArgumentException("Input text cannot be empty", nameof(inputText));
        if (string.IsNullOrEmpty(learningLanguageName))
            throw new ArgumentException("Learning language cannot be empty", nameof(learningLanguageName));
        if (string.IsNullOrEmpty(agent.BaseUrl) || string.IsNullOrEmpty(agent.ApiKey) || string.IsNullOrEmpty(agent.ModelName))
            throw new ArgumentException("At least one of the Agent data is null", nameof(agent));

        try
        {
            // Build the complete API endpoint
            var apiUrl = agent.BaseUrl.TrimEnd('/') + "/chat/completions";

            // Build the prompt
            var userPrompt = $"My input is: {inputText}";
            var systemPrompt = $"""
                                 <language_expert_prompt>
                                     <role>
                                         <description>You are a multilingual language expert with rich life experience in different linguistic and cultural environments. You understand the specific concepts and cultural connotations of various items, behaviors, and things in different languages.</description>
                                         <user>My native language is {nativeLanguageName}, and I am learning {learningLanguageName}. I have no living experience in {learningLanguageName}-speaking countries/regions.</user>
                                     </role>
                                     
                                     <critical_instruction>
                                         <output_language>You MUST respond strictly in {nativeLanguageName} regardless of what language the input text is. ALL explanations, descriptions, example translations, and related vocabulary must be in {nativeLanguageName}. This is the most important requirement.</output_language>
                                     </critical_instruction>
                                     
                                     <task>
                                         <beforehand_check>First, carefully determine what language the input text is. Please note: if the input is a common {learningLanguageName} word, even if similar words might exist in other languages, it should be primarily recognized as {learningLanguageName}.</beforehand_check>
                                         <scenario_1>
                                            <condition>If the input is in {learningLanguageName} (the language the user is learning)</condition>
                                 			<action>Explain the meaning of the input in {nativeLanguageName} in an easy-to-understand way, including its common meanings in different fields and contexts. Add cultural background if necessary.</action>
                                         </scenario_1>
                                         
                                         <scenario_2>
                                            <condition>If the input is clearly not in {learningLanguageName}, but in another language</condition>
                                 			<action>Tell me in {nativeLanguageName} what the input is usually called in {learningLanguageName}. If there are multiple similar expressions, list them all and explain the usage scenarios.</action>
                                         </scenario_2>
                                     </task>
                                     
                                     <format_requirements>
                                         <structure>User input text with phonetic transcription + {nativeLanguageName} explanation + {learningLanguageName} example sentences + closely related {learningLanguageName} vocabulary and explanations</structure>
                                         <multiple_meanings>If there are significantly different meanings, explain them separately</multiple_meanings>
                                         <formatting>
                                             <requirement>Clear paragraph breaks</requirement>
                                             <requirement>Reasonable use of bold headings for easy reading</requirement>
                                            <requirement>Do not use code block format, output markdown content directly</requirement>
                                            <requirement>OUTPUT FORMAT RULE: The very first non-empty line MUST be the canonical word only, wrapped in double asterisks, for example: **apple**. There must be no other text on that line. If the input appears misspelled, choose the closest correct canonical word and output it on the first line wrapped in double asterisks.</requirement>
                                         </formatting>
                                     </format_requirements>
                                     
                                     <response_example>
                                         <native_to_target>**example word**  
                                 "example word" in German is generally called "Beispielwort" /ˈbaɪʃpiːlvɔʁt/

                                 Other expressions:
                                 - Musterwort /ˈmʊstɐvɔʁt/ - more formal expression
                                 - Demowort /ˈdemoːvɔʁt/ - more colloquial expression</native_to_target>
                                         <target_to_native>**example**  
                                 **example** /ɪɡˈzæmpl/ means "instance, illustration"

                                 **Meaning explanation:**
                                 It refers to a specific case used to illustrate or prove a point, rule, or concept. It has different applications in various fields.

                                 **Example sentences:**
                                 - Can you give me an example of how this works? (Can you give me an example of how this works?)
                                 - This painting is a perfect example of Renaissance art. (This painting is a perfect example of Renaissance art.)
                                 - For example, you could try using a different approach. (For example, you could try using a different approach.)

                                 **Related vocabulary:**
                                 - instance /ˈɪnstəns/: instance, more formal expression
                                 - sample /ˈsæmpl/: sample, specimen
                                 - illustration /ˌɪləˈstreɪʃn/: illustration, example
                                 - case /keɪs/: case, situation</target_to_native>
                                     </response_example>
                                     
                                     <important_reminders>
                                         1. If specific cultural concepts are involved, briefly provide cultural background, otherwise skip it directly without unnecessary elaboration
                                         2. Use International Phonetic Alphabet (IPA) format
                                         3. Do not include any introductory text or ask questions at the end
                                     </important_reminders>
                                 </language_expert_prompt>
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
                temperature = 0.1f,
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
                return new ExplanationResult
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
                return new ExplanationResult
                {
                    IsSuccess = false,
                    ModelName = agent.ModelName,
                    ErrorMessage = $"API response contains no choices. Full response: {fullResponse}"
                };
            }

            var firstChoice = apiResponse.Choices[0];
            if (firstChoice?.Message?.Content == null || string.IsNullOrWhiteSpace(firstChoice.Message.Content))
            {
                var fullResponse = JsonSerializer.Serialize(apiResponse);
                return new ExplanationResult
                {
                    IsSuccess = false,
                    ModelName = agent.ModelName,
                    ErrorMessage = $"API response choice contains no valid content. Full response: {fullResponse}"
                };
            }

            return new ExplanationResult
            {
                IsSuccess = true,
                Markdown = firstChoice.Message.Content.Trim(),
                ModelName = $"{agent.Provider}:{agent.ModelName}",
            };
        }
        catch (Exception ex)
        {
            return new ExplanationResult
            {
                IsSuccess = false,
                ModelName = agent.ModelName,
                ErrorMessage = $"An exception occurred: {ex.Message}"
            };
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
        ExplanationResult result = new ExplanationResult();
        foreach (var agent in configurationService.Agents)
        {
            result = await GetMarkdownExplanationAsync(inputText, nativeLanguage, targetLanguage, agent);
            if (result.IsSuccess)
            {
                return result;
            }
        }

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
