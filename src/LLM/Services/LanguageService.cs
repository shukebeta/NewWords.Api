using Flurl.Http;
using LLM.Models;

namespace LLM.Services;

/// <summary>
/// Language Service, fully utilizing Flurl features
/// </summary>
public class LanguageService()
{
    /// <summary>
    /// Get Markdown formatted explanation using Flurl features
    /// </summary>
    /// <param name="inputText">Input text</param>
    /// <param name="targetLanguage">Target language</param>
    /// <param name="apiBaseUrl">API base URL</param>
    /// <param name="apiKey">API key</param>
    /// <param name="model">Model name</param>
    /// <returns>Explanation result</returns>
    public async Task<ExplanationResult> GetMarkdownExplanationAsync(
        string inputText,
        string nativeLanguage,
        string targetLanguage,
        string apiBaseUrl,
        string apiKey,
        string model)
    {
        // Validate input
        if (string.IsNullOrEmpty(inputText)) throw new ArgumentException("Input text cannot be empty", nameof(inputText));
        if (string.IsNullOrEmpty(targetLanguage)) throw new ArgumentException("Target language cannot be empty", nameof(targetLanguage));
        if (string.IsNullOrEmpty(apiBaseUrl)) throw new ArgumentException("API base URL cannot be empty", nameof(apiBaseUrl));
        if (string.IsNullOrEmpty(apiKey)) throw new ArgumentException("API key cannot be empty", nameof(apiKey));
        if (string.IsNullOrEmpty(model)) throw new ArgumentException("Model name cannot be empty", nameof(model));

        try
        {
            // Build the complete API endpoint
            string apiUrl = apiBaseUrl.TrimEnd('/') + "/chat/completions";

            // Build the prompt
            var userPrompt = $@"nativeLanguage={nativeLanguage} 
targetLanguage={targetLanguage}
你是一个精通多种语言的语言专家，在不同语言文化环境中都有丰富的生活经验，你了解各种物品、行为、事物在不同语言中的具体概念和文化内涵。
我的母语是 {nativeLanguage}，我正在学习 {targetLanguage}，我在 {targetLanguage} 国家/地区没有生活经验。
任务要求 如果我给你一个 {targetLanguage} 词汇或短语：
用 {nativeLanguage} 向我解释这个词汇的含义 解释它在不同领域、不同语境下的常见含义 提供文化背景信息（如适用）
如果我给你一个 {nativeLanguage} 词汇或短语：
用 {nativeLanguage} 告诉我在 {targetLanguage} 里这通常被称为什么 如果有多个对应表达，请都列出来并说明使用场景
格式要求
{targetLanguage} 词汇（包含音标）+ {nativeLanguage} 解释 + {targetLanguage} 例句 + 紧密相关的 {targetLanguage} 词汇和解释；如果有相差较大的不同含义，请分别解释；要清晰的分段，合理使用粗体标题，以易于阅读。不要使用代码块格式，直接输出markdown内容。
格式范例 当我给你母语词汇""示例词汇""时，你应该这样响应：""示例词汇""通常被称为""example word"" /ɪɡˈzæmpl wɜːrd/ 如果有多个表达方式，会列出：
sample term /ˈsæmpl tɜːrm/ - 更正式的表达 demo word /ˈdemoʊ wɜːrd/ - 更口语化的表达
举例来说，当我给你 {targetLanguage} 词汇 example 时，你应该这样回应：
example /ɪɡˈzæmpl/ 的意思是""示例、例子""
意思解释： 它指的是用来说明或证明某个观点、规则或概念的具体实例。在不同领域有不同的应用场景。
例句：
Can you give me an example of how this works? 你能给我举个例子说明这是如何工作的吗？ This painting is a perfect example of Renaissance art. 这幅画是文艺复兴艺术的完美范例。 For example, you could try using a different approach. 例如，你可以尝试使用不同的方法。
相关词汇：
instance /ˈɪnstəns/：实例，更正式的表达 sample /ˈsæmpl/：样本，样例 illustration /ˌɪləˈstreɪʃn/：说明，例证 case [keɪs]：案例，情况
重要提醒 如果涉及文化特定概念，请提供简短的文化背景。音标使用国际音标(IPA)格式。

我的输入是：: {inputText}";

            var systemPrompt = "注意：只输出markdown格式的内容，不要包含任何介绍性文字或代码块标记";

            // Build the request body
            var requestData = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3f
            };

            // Use Flurl's fluent API to send request and directly parse JSON response
            var response = await apiUrl
                .WithHeader("Authorization", $"Bearer {apiKey}")
                .PostJsonAsync(requestData)
                .ReceiveJson<ApiCompletionResponse>();

            // Parse the response
            if (response?.Choices != null && response.Choices.Length > 0 &&
                !string.IsNullOrWhiteSpace(response.Choices[0].Message.Content))
            {
                return new ExplanationResult
                {
                    IsSuccess = true,
                    Markdown = response.Choices[0].Message.Content.Trim(),
                    ModelName = model
                };
            }

            return new ExplanationResult
            {
                IsSuccess = false,
                ModelName = model,
                ErrorMessage = "No valid content in the API response"
            };
        }
        catch (FlurlHttpException ex)
        {
            // Use Flurl's exception handling to get detailed information
            string errorDetail = await ex.GetResponseStringAsync().ConfigureAwait(false);

            return new ExplanationResult
            {
                IsSuccess = false,
                HttpStatusCode = ex.StatusCode,
                ModelName = model,
                ErrorMessage = $"HTTP error {ex.StatusCode}: {ex.Message}. Details: {errorDetail}"
            };
        }
        catch (Exception ex)
        {
            return new ExplanationResult
            {
                IsSuccess = false,
                ModelName = model,
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
        string targetLanguage,
        string apiBaseUrl,
        string apiKey,
        string[] models)
    {
        if (models == null || models.Length == 0)
            throw new ArgumentException("At least one model must be provided", nameof(models));

        foreach (var model in models)
        {
            var result = await GetMarkdownExplanationAsync(inputText, nativeLanguage, targetLanguage, apiBaseUrl, apiKey, model);
            if (result.IsSuccess)
                return result;
        }

        // All models failed, return the last error
        return new ExplanationResult
        {
            IsSuccess = false,
            ModelName = models.LastOrDefault(),
            ErrorMessage = "No valid model provided"
        };
    }
}
