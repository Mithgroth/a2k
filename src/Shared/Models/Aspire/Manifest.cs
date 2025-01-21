using System.Text.Json.Serialization;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents manifest.json output of dotnet run --publisher manifest --output-path manifest.json command on AppHost project
/// </summary>
public sealed record Manifest
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("resources")]
    public Dictionary<string, ManifestResource> Resources { get; set; } = [];
}

public sealed record ManifestResource
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
    [JsonPropertyName("image")]
    public string Image { get; set; }
}

public sealed record ResourceBinding
{
    [JsonPropertyName("scheme")]
    public string Scheme { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("transport")]
    public string Transport { get; set; }

    [JsonPropertyName("external")]
    public bool? External { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("targetPort")]
    public int? TargetPort { get; set; }
}

public sealed record ResourceBuild
{
    [JsonPropertyName("context")]
    public string Context { get; set; }

    [JsonPropertyName("dockerfile")]
    public string Dockerfile { get; set; }
}

public sealed record ResourceInput
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("secret")]
    public bool Secret { get; set; }

    [JsonPropertyName("default")]
    public DefaultValue Default { get; set; }
}

public sealed record DefaultValue
{
    [JsonPropertyName("generate")]
    public GenerateInfo Generate { get; set; }
}

public sealed record GenerateInfo
{
    [JsonPropertyName("minLength")]
    public int MinLength { get; set; }
}