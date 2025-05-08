using System.Text.Json;

namespace Api.Framework.Options;
public static class JsonOptions
{
    public static JsonSerializerOptions CaseInsensitive { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
