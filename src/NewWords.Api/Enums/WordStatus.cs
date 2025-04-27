namespace NewWords.Api.Enums
{
    /// <summary>
    /// Represents the learning status of a word for a specific user.
    /// </summary>
    public enum WordStatus
    {
        /// <summary>
        /// The word has been added but not yet actively studied.
        /// </summary>
        New = 0,

        /// <summary>
        /// The user is currently learning this word.
        /// </summary>
        Learning = 1,

        /// <summary>
        /// The user feels they have mastered this word.
        /// </summary>
        Mastered = 2
    }
}