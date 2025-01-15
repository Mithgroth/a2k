using System.Text.Json.Serialization;

namespace a2k.Cli.Models;

public class ResourceBinding
{
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("transport")]
    public string Transport { get; set; }

    [JsonPropertyName("external")]
    public bool? External { get; set; }

    [JsonPropertyName("targetPort")]
    public int? TargetPort { get; set; }
}