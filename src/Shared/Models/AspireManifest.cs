using System.Text.Json.Serialization;

namespace a2k.Shared.Models;

/// <summary>
/// Represents manifest.json output of dotnet run --publisher manifest --output-path manifest.json command on AppHost project
/// </summary>
public class AspireManifest
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("resources")]
    public Dictionary<string, AspireResource>? Resources { get; set; }
}
