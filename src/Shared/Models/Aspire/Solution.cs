using k8s;
using Spectre.Console;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace a2k.Shared.Models.Aspire;

public sealed record Solution
{
    /// <summary>
    /// Represents .sln file name in a .NET Aspire solution, used as Namespace in Kubernetes
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Environment name for the deployment - default is "default"
    /// Can be used to seperate "dev", "stg", "prod" environments
    /// Used as Application in Kubernetes
    /// </summary>
    public string Env { get; set; } = "default";
    /// <summary>
    /// AppHost folder path of the .NET Aspire solution
    /// </summary>
    public string AppHostPath { get; set; } = string.Empty;
    /// <summary>
    /// If true, a2k starts overriding Docker image tags and keeps the old images
    /// In Kubernetes, new versions will be under "revisions"
    /// If false, a2k overwrites the existing resources
    /// </summary>
    public bool UseVersioning { get; } = false;
    /// <summary>
    /// manifest.json path of the AppHost project
    /// </summary>
    public string ManifestPath { get; set; } = string.Empty;
    /// <summary>
    /// manifest.json content of the AppHost project
    /// </summary>
    public Manifest? Manifest { get; set; }
    /// <summary>
    /// If UseVersioning is false, it defaults to "latest" for Docker images
    /// If UseVersioning is true, a2k will use a version format of:
    /// YY + day of year + HHmmss, like:
    /// 25 020 170233 --> 25020170233
    /// Tag will be used in Docker images and Kubernetes metadata
    /// </summary>
    public string Tag { get; set; } = "latest";
    /// <summary>
    /// Resources parsed from manifest.json
    /// </summary>
    public List<Resource> Resources { get; set; } = [];

    private readonly Dictionary<string, string> _resolvedValues = [];
    private readonly Dictionary<string, string> _generatedSecrets = [];

    public Solution(string appHostPath, string? name, string env, bool useVersioning = false)
    {
        AppHostPath = appHostPath ?? throw new ArgumentNullException(nameof(appHostPath));
        Env = string.IsNullOrEmpty(env) ? "default" : env;
        ManifestPath = Path.Combine(appHostPath, "manifest.json");
        Name = string.IsNullOrEmpty(name) ? Utility.FindAndFormatSolutionName(appHostPath) : name;
        Tag = useVersioning ? Utility.GenerateVersion() : "latest";
        UseVersioning = useVersioning;
    }

    public Result CreateManifestIfNotExists()
    {
        if (!File.Exists(ManifestPath))
        {
            Shell.Run("dotnet run --publisher manifest --output-path manifest.json");

            return new(Outcome.Created, "manifest.json");
        }
        else
        {
            return new(Outcome.Exists,
            [
                new Rows(
                    new Markup($"[dim]Loading manifest from:[/]"),
                    new TextPath(ManifestPath)
                        .RootColor(Color.Wheat4)
                        .SeparatorColor(Color.White)
                        .StemColor(Color.Wheat4)
                        .LeafColor(Color.Yellow)
                )
            ]);
        }
    }

    public async Task<Result> ReadManifest()
    {
        var json = await File.ReadAllTextAsync(ManifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(json, Defaults.JsonSerializerOptions);
        if (manifest is null)
        {
            return new(Outcome.Missing, 
            [
                new Markup($"[red]Could not read manifest.json file at {ManifestPath}[/]")
            ]);
        }

        if (manifest?.Schema != Defaults.ASPIRE_SCHEMA)
        {
            return new(Outcome.Failed,
            [
                new Markup($"[red]Incorrect/missing $schema entry in manifest.json file at {ManifestPath}, .NET Aspire manifests are supposed to have \"$schema\":\"{Defaults.ASPIRE_SCHEMA}\" entry in manifest.[/]")
            ]);
        }

        if (manifest?.Resources.Count == 0)
        {
            return new(Outcome.Failed,
            [
                new Markup($"[red]Could not find any resources in manifest.json file at {ManifestPath}.[/]")
            ]);
        }

        Manifest = manifest;

        foreach (var (resourceName, manifestResource) in Manifest!.Resources)
        {
            var resource = ToResource(resourceName, manifestResource);
            Resources.Add(resource);
        }

        ProcessBindings();

        return new(Outcome.Succeeded,
        [
            new Markup($"[bold blue]manifest.json loaded![/]")
        ]);

        Resource ToResource(string resourceName, ManifestResource manifestResource)
            => manifestResource.ResourceType switch
            {
                var rt when rt.Contains("project", StringComparison.OrdinalIgnoreCase)
                    => new Project(this,
                                   resourceName,
                                   CsProjPath: Path.GetFullPath(Path.Combine(AppHostPath, manifestResource.Path)),
                                   UseVersioning,
                                   new Dockerfile($"{Name.ToLowerInvariant()}-{resourceName.ToLowerInvariant()}", Tag),
                                   manifestResource.Bindings,
                                   manifestResource.Env),
                var rt when rt.Contains("container", StringComparison.OrdinalIgnoreCase)
                    => new Container(this,
                                     resourceName,
                                     UseVersioning,
                                     Dockerfile: manifestResource switch
                                     {
                                         var r when !string.IsNullOrEmpty(r.Image) => new Dockerfile(r.Image),
                                         var r when r.Build != null => CreateDockerfile(r, AppHostPath, resourceName),
                                         _ => throw new ArgumentException(nameof(resourceName)),
                                     },
                                     manifestResource.Bindings,
                                     manifestResource.Env),
                var rt when rt.Contains("parameter", StringComparison.OrdinalIgnoreCase)
                    => new Parameter(this,
                                     resourceName,
                                     manifestResource.Value,
                                     manifestResource.Inputs),
                var rt when rt.Contains("value", StringComparison.OrdinalIgnoreCase)
                    => new Value(this,
                                 resourceName,
                                 manifestResource.Value,
                                 manifestResource.Inputs),
                _ => throw new ArgumentException($"Unknown resource type: {manifestResource.ResourceType} for resource {resourceName}")
            };
        Dockerfile CreateDockerfile(ManifestResource r, string appHostPath, string resourceName)
        {
            var context = Path.Combine(appHostPath, r.Build.Context) ?? ".";
            var path = Path.Combine(appHostPath, r.Build.Dockerfile.Replace("/", "\\") ?? "Dockerfile");

            return new Dockerfile(resourceName,
                                  Tag: Tag,
                                  Context: context,
                                  Path: path,
                                  ShouldBuildWithDocker: true);
        }
    }

    public async Task<Result> CheckNamespace(k8s.Kubernetes k8s, bool shouldCreateIfNotExists = true)
    {
        try
        {
            await k8s.ReadNamespaceAsync(Name);
            return new(Outcome.Exists, Name);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!shouldCreateIfNotExists)
            {
                return new(Outcome.Missing, Name);
            }

            await k8s.CreateNamespaceAsync(Defaults.V1Namespace(Name, Env, Tag));
            return new(Outcome.Created, Name);
        }
    }

    public IReadOnlyList<(Resource Resource, int Port)> GetExternalBindings()
    {
        return Resources
            .Where(r => r is Project)  // Only look at Project resources
            .Cast<Project>()
            .SelectMany(p => p.Bindings?.Values
                .Where(b => (b.External ?? false) && b.Scheme == "https")  // Only HTTPS bindings
                .Select(_ => (Resource: p as Resource, Port: p.GetProjectPort() ?? 80)) 
                ?? [])
            .ToList();
    }

    private void ProcessBindings()
    {
        // First, resolve all parameters and secrets
        foreach (var resource in Resources.OfType<Parameter>())
        {
            var manifestResource = Manifest!.Resources[resource.ResourceName];
            if (manifestResource.Value.Contains("inputs.value"))
            {
                var input = manifestResource.Inputs["value"];
                if (input.Secret && input.Default?.Generate != null)
                {
                    _generatedSecrets[resource.ResourceName] = Utility.GenerateSecureString(input.Default.Generate.MinLength);
                }
            }
        }

        // Then, resolve all connection strings
        foreach (var resource in Resources)
        {
            var manifestResource = Manifest!.Resources[resource.ResourceName];
            if (!string.IsNullOrEmpty(manifestResource.ConnectionString))
            {
                _resolvedValues[$"{resource.ResourceName}.connectionString"] = ResolveConnectionString(manifestResource.ConnectionString);
            }
        }

        // Finally, update all resources with resolved values
        foreach (var resource in Resources)
        {
            if (resource.Env != null)
            {
                foreach (var (key, value) in resource.Env.ToList())
                {
                    resource.Env[key] = ResolveValue(value);
                }
            }
        }
    }

    private string ResolveConnectionString(string template)
    {
        // First resolve any bindings in the template
        var resolvedTemplate = ResolveValue(template);
        
        // If this is a fully resolved string, return it
        if (!resolvedTemplate.Contains('{'))
        {
            return resolvedTemplate;
        }

        // Otherwise, try one more pass to resolve any remaining placeholders
        return ResolveValue(resolvedTemplate);
    }

    private string ResolveValue(string value)
    {
        return !value.Contains('{')
            ? value
            : Regex.Replace(value, @"\{([^}]+)\}", match =>
        {
            var placeholder = match.Groups[1].Value;
            var parts = placeholder.Split('.');

            if (parts.Length < 2)
            {
                return match.Value;
            }

            var resourceName = parts[0];
            var bindingType = parts[1];

            return bindingType switch
            {
                "value" => _generatedSecrets.GetValueOrDefault(resourceName, match.Value),
                "connectionString" => _resolvedValues.GetValueOrDefault($"{resourceName}.connectionString", match.Value),
                "bindings" => ResolveBinding(resourceName, parts[2], parts[3]),
                _ => match.Value
            };
        });
    }

    private string ResolveBinding(string resourceName, string bindingName, string property)
    {
        var resource = Resources.First(r => r.ResourceName == resourceName);
        var binding = resource.Bindings?[bindingName] 
            ?? throw new InvalidOperationException($"No binding {bindingName} found for resource {resourceName}");

        // For tcp bindings (like postgres), always use targetPort
        if (binding.Protocol == "tcp" && binding.Transport != "http")
        {
            return property switch
            {
                "host" => $"{resourceName}-service.{Name}.svc.cluster.local",
                "port" => binding.TargetPort?.ToString() ?? binding.Port?.ToString() ?? "80",
                "targetPort" => binding.TargetPort?.ToString() ?? "80",
                _ => throw new InvalidOperationException($"Unknown binding property {property}")
            };
        }

        // For http/https bindings
        return property switch
        {
            "host" => $"{resourceName}.{Name}.local",
            "port" => binding.Port?.ToString() ?? binding.TargetPort?.ToString() ?? "80",
            "targetPort" => binding.TargetPort?.ToString() ?? "80",
            "url" => $"{binding.Scheme}://{resourceName}.{Name}.local:{binding.Port ?? binding.TargetPort ?? 80}",
            _ => throw new InvalidOperationException($"Unknown binding property {property}")
        };
    }
}
