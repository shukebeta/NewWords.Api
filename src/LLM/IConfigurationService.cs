using LLM.Models;

namespace LLM;

public interface IConfigurationService
{
    List<Agent> Agents { get; }
    List<Language> SupportedLanguages { get; }
    string? GetLanguageName(string code);
}