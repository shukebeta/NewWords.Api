using NewWords.Api.Models.DTOs;

namespace NewWords.Api.Helpers;

public class LanguageHelper(IConfiguration configuration)
{
    private readonly List<LanguageDto> _supportedLanguages = configuration.GetSection("SupportedLanguages").Get<List<LanguageDto>>()!;

    /// <summary>
    /// Retrieves the list of supported languages.
    /// </summary>
    /// <returns>A list of LanguageDto objects containing language codes and names.</returns>
    public List<LanguageDto> GetSupportedLanguages()
    {
        return _supportedLanguages;
    }

    /// <summary>
    /// Gets the language name based on the provided language code.
    /// </summary>
    /// <param name="code">The language code to look up.</param>
    /// <returns>The name of the language if found; otherwise, null.</returns>
    public string? GetLanguageName(string code)
    {
        var language = _supportedLanguages.FirstOrDefault(l => l.Code == code);
        return language?.Name;
    }
}
