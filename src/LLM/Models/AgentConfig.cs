namespace LLM.Models;

public class AgentConfig
{
    public string Provider { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public List<string> Models { get; set; } = [];
}