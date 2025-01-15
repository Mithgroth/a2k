using System.Text.Json.Serialization;

namespace a2k.Cli.Models;

public class AspireManifest
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; }

    [JsonPropertyName("resources")]
    public Dictionary<string, AspireResource> Resources { get; set; }
}
