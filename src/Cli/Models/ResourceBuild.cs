using System.Text.Json.Serialization;

namespace a2k.Cli.Models;

public class ResourceBuild
{
    [JsonPropertyName("context")]
    public string Context { get; set; }

    [JsonPropertyName("dockerfile")]
    public string Dockerfile { get; set; }
}