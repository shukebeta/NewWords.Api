using System.Text;
using System.Text.Json;
using LLM.Configuration;
using LLM.Models;
using System.Net.Http.Headers;
using Api.Framework.Options; // Added for AuthenticationHeaderValue

namespace LLM.Services
{
    /// <summary>
    /// Service for providing phonetic transcription, explanations, and translations
    /// of words or phrases using OpenRouter's API via separate, focused methods.
    /// </summary>
    public class TranslationAndExplanationService
    {
        private readonly HttpClient _httpClient;
        private readonly LlmConfigurationService _configService;
        private readonly string _apiUrl = "https://openrouter.ai/api/v1/chat/completions";

        /// <summary>
        /// Initializes a new instance of the <see cref="TranslationAndExplanationService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for making API requests.</param>
        /// <param name="configService">The configuration service for accessing API settings.</param>
        public TranslationAndExplanationService(HttpClient httpClient, LlmConfigurationService configService)
        {
            _httpClient = httpClient;
            _configService = configService;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configService.ApiKey);
        }

        /// <summary>
        /// Gets a Markdown-formatted explanation for the input text in the target language.
        /// </summary>
        /// <param name="inputText">The word or phrase to explain.</param>
        /// <param name="targetLanguage">The language for the explanation.</param>
        /// <returns>A string containing the Markdown explanation.</returns>
        /// <exception cref="ArgumentException">Thrown if input text or target language is empty.</exception>
        /// <exception cref="Exception">Thrown if all configured models fail.</exception>
        public async Task<string> GetMarkdownExplanationAsync(string inputText, string targetLanguage)
        {
            ValidateInput(inputText, targetLanguage);
            string currentModel = _configService.GetPrimaryModel();

            while (!string.IsNullOrEmpty(currentModel))
            {
                try
                {
                    var apiResponse = await _MakeMarkdownApiRequestAsync(inputText, targetLanguage, currentModel);
                    // For Markdown, we expect the raw content string directly
                    var responseObj = JsonSerializer.Deserialize<OpenRouterResponse>(apiResponse, JsonOptions.CaseInsensitive);
                    if (responseObj?.Choices != null && responseObj.Choices.Count > 0)
                    {
                        return responseObj.Choices[0].Message.Content.Trim();
                    }
                    // If parsing fails or content is empty, treat as model failure and try fallback
                    throw new Exception($"Failed to parse Markdown response or empty content from model {currentModel}.");
                }
                catch (Exception ex)
                {
                    LogAndSelectFallbackModel(ex, ref currentModel);
                }
            }
            throw new Exception("All configured models failed to provide a Markdown explanation.");
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
            string currentModel = _configService.GetPrimaryModel();

            while (!string.IsNullOrEmpty(currentModel))
            {
                try
                {
                    var apiResponse = await _MakeStructuredApiRequestAsync(inputText, targetLanguage, currentModel);
                    var result = _ParseStructuredExplanationResponse(apiResponse, inputText);
                    // Check if parsing was successful (indicated by non-default values or lack of error message)
                    if (result.TextLanguage != "Unknown" || !string.IsNullOrEmpty(result.PrimaryTranslation))
                    {
                         return result;
                    }
                     // If parsing resulted in default/error state, treat as model failure
                    throw new Exception($"Failed to parse structured JSON response from model {currentModel}.");
                }
                catch (Exception ex)
                {
                    LogAndSelectFallbackModel(ex, ref currentModel);
                }
            }
            throw new Exception("All configured models failed to provide structured explanation data.");
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

        private void LogAndSelectFallbackModel(Exception ex, ref string currentModel)
        {
            // Log the error if needed (replace Console.WriteLine with proper logging if available)
            Console.WriteLine($"Error with model {currentModel}: {ex.Message}");
            currentModel = _configService.GetFallbackModel(currentModel);
            if (!string.IsNullOrEmpty(currentModel))
            {
                Console.WriteLine($"Falling back to model: {currentModel}");
            }
        }

        private async Task<string> _MakeMarkdownApiRequestAsync(string inputText, string targetLanguage, string model)
        {
            var userPrompt = $@"Generate ONLY a concise yet comprehensive explanation formatted in Markdown for the input text ""{inputText}"" in {targetLanguage}.
The explanation MUST follow this structure exactly:

**[Input Text]** \[IPA Transcription] **[Primary Meaning in {targetLanguage}]**

---

### **[{targetLanguage} Explanation]:**

[Detailed explanation of meaning and usage in {targetLanguage}]

---

### **[Example Sentences]:**

* [Sentence 1 in original language]
  [{targetLanguage} translation of Sentence 1]

* [Sentence 2 in original language]
  [{targetLanguage} translation of Sentence 2]
  
(Include 2-3 relevant examples)

---

### **[Related Vocabulary & Phrases]:**

* **[Related Term 1]**: [Meaning/Usage in {targetLanguage}]
* **[Related Term 2]**: [Meaning/Usage in {targetLanguage}]

---

[Optional: A concluding sentence or usage tip in {targetLanguage}]

**IMPORTANT:**
- Respond ONLY with the Markdown content matching the structure above.
- Do NOT include any introductory text, concluding remarks, or code fences (like ```markdown) outside the defined structure.
- Replace bracketed placeholders like `[Input Text]` or `[IPA Transcription]` with the actual information for ""{inputText}"".
- Ensure the IPA transcription is accurate.
- Provide translations and explanations in {targetLanguage}.
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
                temperature = 0.3f // Added temperature setting
            };

            return await _SendApiRequestAsync(requestBody);
        }

        private async Task<string> _MakeStructuredApiRequestAsync(string inputText, string targetLanguage, string model)
        {
             // User prompt focuses on the *content* required, EXCLUDING markdown.
            var userPrompt = $"Generate a detailed linguistic analysis JSON object for the input text: \"{inputText}\". Provide the analysis in {targetLanguage}. Include ONLY the following components: 1. Recognized language of the input text ('textLanguage'). 2. IPA transcription ('ipaTranscription'). 3. Part of speech ('partOfSpeech'). 4. Primary translation ('primaryTranslation'). 5. Alternative translations ('alternativeTranslations', array). 6. Detailed explanation ('detailedExplanation'). 7. Example sentences with translations ('exampleSentences', array of objects with 'original' and 'translation'). 8. Related vocabulary terms ('relatedTerms', array of objects with 'term', 'ipaTranscription', 'partOfSpeech', 'meaning'). DO NOT include an 'explanationInMarkdown' field.";

            // Example JSON structure WITHOUT the markdown field
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
            }"; // Note: explanationInMarkdown is removed

            // System prompt focuses on persona and *format* constraints.
            var systemPrompt = $@"You are a linguistic expert specializing in detailed word/phrase analysis and translation.
Your task is to generate ONLY a valid JSON object containing the linguistic analysis requested in the user prompt.

**Response Format Constraints:**
1.  **MUST respond ONLY with a valid JSON object.**
2.  **Do NOT include any text, explanations, or formatting (like ```json ... ```) before or after the JSON object.**
3.  The JSON object MUST contain ONLY the fields requested in the user prompt, matching the structure shown in the example. DO NOT include 'explanationInMarkdown'.
4.  All fields should contain plain text or the specified JSON structures (arrays/objects).
5.  **CRITICAL JSON Escaping Rules:**
    *   All string values within the JSON MUST be properly escaped according to JSON standards.
    *   Pay special attention to backslashes (`\\`). To include a literal backslash in a JSON string, it MUST be escaped as `\\\\`. For example, if you want `\\[IPA]`, the JSON string must contain `""\\\\[IPA]""`.
    *   Ensure newlines within strings are escaped as `\\\\n`.
    *   Ensure double quotes within strings are escaped as `\\\\""`.

**Example JSON Structure (No Markdown Field):**
```json
{sampleJson}
```
Remember: Your entire response must be ONLY the JSON object, starting with `{{` and ending with `}}`." ;


            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3f // Added temperature setting
            };

             return await _SendApiRequestAsync(requestBody);
        }

         private async Task<string> _SendApiRequestAsync(object requestBody)
        {
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode(); // Throws if status code is not 2xx

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
                        // Populate the result object. ExplanationInMarkdown will be empty/null by default.
                        result.TextLanguage = string.IsNullOrEmpty(explanationResult.TextLanguage) ? "Unknown" : explanationResult.TextLanguage;
                        result.IpaTranscription = explanationResult.IpaTranscription;
                        result.PartOfSpeech = explanationResult.PartOfSpeech;
                        result.PrimaryTranslation = explanationResult.PrimaryTranslation;
                        result.AlternativeTranslations = explanationResult.AlternativeTranslations;
                        result.DetailedExplanation = explanationResult.DetailedExplanation;
                        // result.ExplanationInMarkdown is NOT set here
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
