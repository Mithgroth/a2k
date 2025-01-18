using System.Text.Json;

namespace a2k.Shared;

public static class Defaults
{
    public static JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static Dictionary<string, string> Labels(string applicationName,
                                                    string resourceName)
        => new()
        {
            { "application", applicationName },
            { "deployed-by", "a2k" },
            { "name", resourceName },
        };

}
