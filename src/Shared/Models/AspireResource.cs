using System.Text.Json.Serialization;

namespace Shared.Models;

public class AspireResource
{
    [JsonPropertyName("type")]
    public string ResourceType { get; set; }

    //[JsonPropertyName("connectionString")]
    //public string ConnectionString { get; set; }

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

public class ResourceBuild
{
    [JsonPropertyName("context")]
    public string Context { get; set; }

    [JsonPropertyName("dockerfile")]
    public string Dockerfile { get; set; }
}

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