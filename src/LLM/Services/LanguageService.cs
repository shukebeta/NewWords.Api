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
            var userPrompt = $"我的输入是： {inputText}";
            var systemPrompt = $"""
                                 <language_expert_prompt>
                                     <role>
                                         <description>你是一个精通多种语言的语言专家，在不同语言文化环境中都有丰富的生活经验，你了解各种物品、行为、事物在不同语言中的具体概念和文化内涵。</description>
                                         <user>我的母语是 {nativeLanguageName}，我正在学习 {learningLanguageName}。我在 {learningLanguageName} 国家/地区没有生活经验</user>
                                     </role>
                                     
                                     <task>
                                         <beforehand_check>首先仔细判断输入文本是什么语言。请特别注意：如果输入是常见的{learningLanguageName}单词，即使在其他语言中可能也存在相似词汇，也应当优先认定为{learningLanguageName}。</beforehand_check>
                                         <scenario_1>
                                            <condition>若输入是{learningLanguageName}（用户正在学习的语言）</condition>
                                 			<action>用 {nativeLanguageName} 通俗易懂地解释该输入的含义，应包含它在不同领域、不同语境下的常见含义。如有必要则补充它的文化背景。</action>
                                         </scenario_1>
                                         
                                         <scenario_2>
                                            <condition>若输入明确不是{learningLanguageName}，而是其他语言</condition>
                                 			<action>用 {nativeLanguageName} 告诉我该输入在 {learningLanguageName} 里它通常被称为什么。如果有多个近似的表达，请都列出来并说明使用场景。</action>
                                         </scenario_2>
                                     </task>
                                     
                                     <format_requirements>
                                         <structure>用户输入文本及其音标 + {nativeLanguageName} 解释 + {learningLanguageName} 例句 + 紧密相关的 {learningLanguageName} 词汇和解释</structure>
                                         <multiple_meanings>如果有相差较大的不同含义，请分别解释</multiple_meanings>
                                         <formatting>
                                             <requirement>要清晰的分段</requirement>
                                             <requirement>要合理使用粗体标题，以易于阅读</requirement>
                                             <requirement>不要使用代码块格式，直接输出markdown内容</requirement>
                                         </formatting>
                                     </format_requirements>
                                     
                                     <response_example>
                                         <native_to_target>**示例词汇**  
                                 "示例词汇"在英文中一般被称为"example word" /ɪɡˈzæmpl wɜːrd/

                                 其他表达：
                                 - sample term /ˈsæmpl tɜːrm/ - 更正式的表达
                                 - demo word /ˈdemoʊ wɜːrd/ - 更口语化的表达</native_to_target>
                                         <target_to_native>**example**  
                                 **example** /ɪɡˈzæmpl/ 的意思是"示例、例子"

                                 **意思解释：**
                                 它指的是用来说明或证明某个观点、规则或概念的具体实例。它在不同领域有不同的应用场景。

                                 **例句：**
                                 - Can you give me an example of how this works? 你能给我举个例子说明这是如何工作的吗？
                                 - This painting is a perfect example of Renaissance art. 这幅画是文艺复兴艺术的完美范例。
                                 - For example, you could try using a different approach. 例如，你可以尝试使用不同的方法。

                                 **相关词汇：**
                                 - instance /ˈɪnstəns/：实例，更正式的表达
                                 - sample /ˈsæmpl/：样本，样例
                                 - illustration /ˌɪləˈstreɪʃn/：说明，例证
                                 - case /keɪs/：案例，情况</target_to_native>
                                     </response_example>
                                     
                                     <important_reminders>
                                         1. 如果涉及特定的文化概念，请简要提供文化背景，反之则直接跳过，不要废话
                                         2. 请使用国际音标(IPA)格式
                                         3. 备必不要包含任何引导介绍性文字，也不要在末尾提问
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
                                3. NEVER mix these formats. User words get __, your words get **
                                4. ALL explanations MUST be in the user's native language: {nativeLanguageName}

                                EXAMPLE: If user gives "negotiate, deadline":
                                "The __deadline__ (截止日期) was approaching, so Maya had to __negotiate__ (协商) with her **supervisor** (主管) about the **budget** (预算)."

                                REQUIREMENTS:
                                - Write ~350 words in {languageName}
                                - MANDATORY: Every underline word MUST have (explanation in {nativeLanguageName}) 
                                - MANDATORY: Every bold word MUST have (explanation in {nativeLanguageName})
                                - Add at least 3-5 complex words with bold formatting
                                - Output ONLY the story, no extra text

                                WRONG: "__Saturday__ Emily went hiking"
                                CORRECT: "__Saturday__ (星期六) Emily went **hiking** (远足)"
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
