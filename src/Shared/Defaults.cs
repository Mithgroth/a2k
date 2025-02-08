using a2k.Shared.Models.Aspire;
using a2k.Shared.Models.Kubernetes;
using k8s.Models;
using System.Text;
using System.Text.Json;

namespace a2k.Shared;

public static class Defaults
{
    public const string ROOT = "[bold lightseagreen]Deploying resources to Kubernetes[/]";
    public const string PHASE_I = "[bold underline mediumpurple1]Phase I   - Preparing[/]";
    public const string PHASE_II = "[bold underline mediumpurple1]Phase II  - Deploying Resources[/]";
    public const string PHASE_III = "[bold underline mediumpurple1]Phase III - Deploying Services for Resources[/]";
    public const string PHASE_IV = "[bold underline mediumpurple1]Phase IV  - Configuring Ingress Bindings[/]";
    public const string PHASE_V = "[bold underline mediumpurple1]Phase V   - Testing Node Status[/]";

    public const string ASPIRE_SCHEMA = "https://json.schemastore.org/aspire-8.0.json";
    public const string LAUNCH_SETTINGS_SCHEMA = "http://json.schemastore.org/launchsettings.json";

    public static string ImageCachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "a2k", "docker-images.json");

    public static JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    public static Dictionary<string, string> Labels(string env, string version)
        => new()
        {
            { "app.kubernetes.io/name", env},
            { "app.kubernetes.io/managed-by", "a2k"},
            { "app.kubernetes.io/version", version}
        };

    public static V1Namespace V1Namespace(string name, string env, string version)
        => new()
        {
            Metadata = new V1ObjectMeta
            {
                Name = name,
                Labels = Labels(env, version),
            }
        };

    public static V1Deployment V1Deployment(string resourceName, string env, string version)
        => new()
        {
            ApiVersion = "apps/v1",
            Kind = Kinds.Deployment.ToString(),
            Metadata = new()
            {
                Name = resourceName,
                Labels = Labels(env, version)
            }
        };

    public static V1Service V1Service(string resourceName, string env, string version)
        => new()
        {
            ApiVersion = "v1",
            Kind = Kinds.Service.ToString(),
            Metadata = new()
            {
                Name = $"{resourceName}-service",
                Labels = Labels(env, version)
            }
        };

    public static V1Secret V1Secret(string resourceName, string env, string version, string data)
        => new()
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                Labels = Labels(env, version)
            },
            StringData = new Dictionary<string, string>
            {
                ["value"] = data
            }
        };

    public static V1ConfigMap V1ConfigMap(string resourceName, string env, string version, string data)
        => new()
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                Labels = Labels(env, version)
            },
            Data = new Dictionary<string, string>
            {
                ["value"] = data
            }
        };

    public static V1LabelSelector V1LabelSelector(IDictionary<string, string> labels)
        => new()
        {
            MatchLabels = labels,
        };

    public static V1Secret ImagePullSecret(Solution solution)
    {
        return new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = "a2k-registry-creds",
                NamespaceProperty = solution.Name
            },
            Type = "kubernetes.io/dockerconfigjson",
            Data = new Dictionary<string, byte[]>
            {
                [".dockerconfigjson"] = Encoding.UTF8.GetBytes($$"""
                    {
                        "auths": {
                            "{{solution.RegistryUrl}}": {
                                "username": "{{solution.RegistryUser}}",
                                "password": "{{solution.RegistryPassword}}",
                                "auth": "{{Convert.ToBase64String(Encoding.UTF8.GetBytes($"{solution.RegistryUser}:{solution.RegistryPassword}"))}}"
                            }
                        }
                    }
                    """)
            }
        };
    }
}
