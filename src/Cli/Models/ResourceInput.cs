using System.Text.Json.Serialization;

namespace a2k.Cli.Models;

public class ResourceInput
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("secret")]
    public bool Secret { get; set; }

    [JsonPropertyName("default")]
    public DefaultValue Default { get; set; }
}

public class DefaultValue
{
    [JsonPropertyName("generate")]
    public GenerateInfo Generate { get; set; }
}

public class GenerateInfo
{
    [JsonPropertyName("minLength")]
    public int MinLength { get; set; }
}