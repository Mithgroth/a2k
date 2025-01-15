using System.Text.Json.Serialization;

namespace a2k.Cli.Models;

public class AspireResource
{
    [JsonPropertyName("type")]
    public string ResourceType { get; set; }

    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("build")]
    public ResourceBuild Build { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; }

    [JsonPropertyName("bindings")]
    public Dictionary<string, ResourceBinding> Bindings { get; set; }

    [JsonPropertyName("inputs")]
    public Dictionary<string, ResourceInput> Inputs { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }
}