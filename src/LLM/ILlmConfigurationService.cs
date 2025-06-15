using LLM.Models;

namespace LLM;

public interface ILlmConfigurationService
{
    List<Agent> Agents { get; }
}