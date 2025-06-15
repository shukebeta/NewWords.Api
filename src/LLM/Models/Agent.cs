namespace LLM.Models;

public class Agent
{
    public string Provider { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } =  string.Empty;
    public string ModelName { get; set; } = string.Empty;
}