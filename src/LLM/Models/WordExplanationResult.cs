namespace LLM.Models
{
    /// <summary>
    /// Represents the result of a word or phrase explanation, including phonetic transcription and translation.
    /// </summary>
    public class WordExplanationResult
    {
        /// <summary>
        /// The original word or phrase that was analyzed.
        /// </summary>
        public string InputText { get; set; } = string.Empty;

        /// <summary>
        /// The language of the input text as recognized by the system.
        /// </summary>
        public string TextLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The International Phonetic Alphabet (IPA) transcription of the word or phrase.
        /// </summary>
        public string IpaTranscription { get; set; } = string.Empty;

        /// <summary>
        /// The part of speech of the word or phrase (e.g., noun, verb).
        /// </summary>
        public string PartOfSpeech { get; set; } = string.Empty;

        /// <summary>
        /// The translation or primary meaning in the target language.
        /// </summary>
        public string PrimaryTranslation { get; set; } = string.Empty;

        /// <summary>
        /// Alternative translations or meanings in the target language, if any.
        /// </summary>
        public List<string> AlternativeTranslations { get; set; } = new List<string>();

        /// <summary>
        /// A detailed explanation of the word or phrase in the target language.
        /// </summary>
        public string DetailedExplanation { get; set; } = string.Empty;

        /// <summary>
        /// Example sentences in the original language with translations in the target language.
        /// </summary>
        public List<ExampleSentence> ExampleSentences { get; set; } = new List<ExampleSentence>();

        /// <summary>
        /// Related vocabulary or terms with their meanings or translations.
        /// </summary>
        public List<RelatedTerm> RelatedTerms { get; set; } = new List<RelatedTerm>();
    }

    /// <summary>
    /// Represents an example sentence with its translation.
    /// </summary>
    public class ExampleSentence
    {
        /// <summary>
        /// The sentence in the original language.
        /// </summary>
        public string Original { get; set; } = string.Empty;

        /// <summary>
        /// The translation of the sentence in the target language.
        /// </summary>
        public string Translation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a related term or vocabulary item with its details.
    /// </summary>
    public class RelatedTerm
    {
        /// <summary>
        /// The related word or term.
        /// </summary>
        public string Term { get; set; } = string.Empty;

        /// <summary>
        /// The International Phonetic Alphabet (IPA) transcription(s) of the term, if available.
        /// </summary>
        public string IpaTranscription { get; set; } = string.Empty; // Changed from List<string> to string

        /// <summary>
        /// The part of speech of the term (e.g., noun, adjective).
        /// </summary>
        public string PartOfSpeech { get; set; } = string.Empty;

        /// <summary>
        /// The meaning or translation of the term in the target language.
        /// </summary>
        public string Meaning { get; set; } = string.Empty;
    }
}