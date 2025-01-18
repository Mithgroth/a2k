﻿using a2k.Shared.Models.Kubernetes;
using k8s.Models;
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

    public static Dictionary<string, string> Labels(string applicationName, string? resourceName = "")
    {
        var defaultLabels = new Dictionary<string, string>()
        {
            { "application", applicationName },
            { "deployed-by", "a2k" },
        };

        if (!string.IsNullOrEmpty(resourceName))
        {
            defaultLabels["name"] = resourceName;
        }

        return defaultLabels;
    }

    public static V1Namespace V1Namespace(string @namespace, string applicationName) => new()
    {
        Metadata = new V1ObjectMeta
        {
            Name = @namespace,
            Labels = Labels(applicationName),
        }
    };

    public static V1Deployment V1Deployment(string applicationName, string resourceName) => new()
    {
        ApiVersion = "apps/v1",
        Kind = Kinds.Deployment.ToString(),
        Metadata = new()
        {
            Name = resourceName,
            Labels = Labels(applicationName, resourceName)
        }
    };

    public static V1Service V1Service(string applicationName, string resourceName) => new()
    {
        ApiVersion = "v1",
        Kind = Kinds.Service.ToString(),
        Metadata = new()
        {
            Name = $"{resourceName}-service",
            Labels = Labels(applicationName, resourceName)
        }
    };

    public static V1LabelSelector V1LabelSelector(IDictionary<string, string> labels) => new()
    {
        MatchLabels = labels,
    };
}
