using System.Collections.Generic;

namespace LLM.Models
{
    /// <summary>
    /// Represents the result of language recognition for a given word or phrase.
    /// </summary>
    public class LanguageRecognitionResult
    {
        /// <summary>
        /// The original text that was analyzed for language recognition.
        /// </summary>
        public string InputText { get; set; } = string.Empty;

        /// <summary>
        /// A list of possible languages with their confidence scores.
        /// </summary>
        public List<LanguageScore> Languages { get; set; } = new List<LanguageScore>();
    }

    /// <summary>
    /// Represents a language and its confidence score from language recognition.
    /// </summary>
    public class LanguageScore
    {
        /// <summary>
        /// The name of the language.
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// The confidence score for this language, typically between 0 and 1.
        /// </summary>
        public double Score { get; set; }
    }
}