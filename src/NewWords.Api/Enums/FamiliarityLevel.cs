namespace NewWords.Api.Enums
{
    /// <summary>
    /// Represents the learning status of a word for a specific user.
    /// </summary>
    public enum FamiliarityLevel
    {
        New = 0,                   // New or never seen before (highest review priority)
        Unfamiliar = 1,            // Not familiar (normal review frequency)
        SomewhatFamiliar = 2,      // Somewhat familiar (reduced review frequency)
        VeryFamiliar = 3,          // Very familiar (no longer reviewed)
    }
}
