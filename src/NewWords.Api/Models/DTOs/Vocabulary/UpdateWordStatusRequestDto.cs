using NewWords.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace NewWords.Api.Models.DTOs.Vocabulary
{
    /// <summary>
    /// Data Transfer Object for updating the learning status of a user's word entry.
    /// </summary>
    public class UpdateWordStatusRequestDto
    {
        /// <summary>
        /// The new learning status for the word.
        /// </summary>
        [Required]
        [EnumDataType(typeof(WordStatus))] // Validate that the value is a valid WordStatus enum member
        public WordStatus NewStatus { get; set; }
    }
}