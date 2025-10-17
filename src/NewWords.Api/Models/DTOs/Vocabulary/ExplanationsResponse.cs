using NewWords.Api.Entities;

namespace NewWords.Api.Models.DTOs.Vocabulary
{
    /// <summary>
    /// Response containing all available explanations for a word
    /// and the user's current default explanation ID.
    /// </summary>
    public class ExplanationsResponse
    {
        /// <summary>
        /// List of all available explanations for the word.
        /// </summary>
        public List<WordExplanation> Explanations { get; set; } = new();

        /// <summary>
        /// The ID of the user's currently selected default explanation.
        /// </summary>
        public long? UserDefaultExplanationId { get; set; }
    }
}
