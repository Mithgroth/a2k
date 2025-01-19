using a2k.Shared.Models.Kubernetes;
using k8s;
using Spectre.Console;
using System.Text.Json;

namespace a2k.Shared.Models.Aspire;

public sealed record Solution
{
    /// <summary>
    /// Represents .sln file name in a .NET Aspire solution, used as Application name in Kubernetes
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string Namespace { get; set; } = "default";

    /// <summary>
    /// AppHost folder path of the .NET Aspire solution
    /// </summary>
    public string AppHostPath { get; set; } = string.Empty;

    /// <summary>
    /// manifest.json path of the AppHost project
    /// </summary>
    public string ManifestPath { get; set; } = string.Empty;
    public Manifest? Manifest { get; set; }

    public List<Resource> Resources { get; set; } = [];

    public Solution(string appHost, string? @namespace)
    {
        // TODO: Ensure it is a .NET Aspire solution

        AppHostPath = appHost ?? throw new ArgumentNullException(nameof(appHost));
        ManifestPath = Path.Combine(appHost, "manifest.json");
        Name = Path.GetFileName(Directory.GetParent(appHost)?.FullName ?? "aspire-app").Replace(".sln", string.Empty);

        if (!string.IsNullOrEmpty(@namespace))
        {
            Namespace = @namespace;
        }
    }

    public void CreateManifestIfNotExists()
    {
        if (!File.Exists(ManifestPath))
        {
            AnsiConsole.MarkupLine($"[yellow]manifest.json file not found at {ManifestPath}, creating...[/]");
            Shell.Run("dotnet run --publisher manifest --output-path manifest.json");
            AnsiConsole.MarkupLine("[green]manifest.json file created![/]");
        }
    }

    public async Task<ResourceOperationResult> ReadManifest()
    {
        CreateManifestIfNotExists();

        var path = new TextPath(ManifestPath)
            .RootColor(Color.Wheat4)
            .SeparatorColor(Color.White)
            .StemColor(Color.Wheat4)
            .LeafColor(Color.Yellow);

        var panel = new Panel(path)
        {
            Header = new("Loading manifest from")
        };

        AnsiConsole.Write(panel);

        var json = await File.ReadAllTextAsync(ManifestPath);

        var manifest = JsonSerializer.Deserialize<Manifest>(json, Defaults.JsonSerializerOptions);
        if (manifest is null)
        {
            AnsiConsole.MarkupLine($"[red]Could not read manifest.json file at {ManifestPath}, make sure the file is a proper .NET Aspire manifest file.[/]");
            return ResourceOperationResult.Missing;
        }

        if (manifest?.Schema != Defaults.ASPIRE_SCHEMA)
        {
            AnsiConsole.MarkupLine($"[red]Incorrect/missing $schema entry in manifest.json file at {ManifestPath}, .NET Aspire manifests are supposed to have \"$schema\":\"{Defaults.ASPIRE_SCHEMA}\" entry in manifest.[/]");
            return ResourceOperationResult.Failed;
        }

        if (manifest?.Resources.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]Could not find any resources in manifest.json file at {ManifestPath}.[/]");
            return ResourceOperationResult.Failed;
        }

        Manifest = manifest;

        foreach (var (resourceName, manifestResource) in manifest.Resources)
        {
            var resource = ToResource(resourceName, manifestResource);
            Resources.Add(resource);
        }

        return ResourceOperationResult.Succeeded;

        Resource ToResource(string resourceName, ManifestResource manifestResource)
            => manifestResource.ResourceType switch
            {
                var rt when rt.Contains("project", StringComparison.OrdinalIgnoreCase)
                    => new Project(Namespace,
                                   Name,
                                   resourceName,
                                   CsProjPath: Path.GetFullPath(Path.Combine(AppHostPath, manifestResource.Path)),
                                   new Dockerfile($"{Name.ToLowerInvariant()}-{resourceName.ToLowerInvariant()}", "latest"),
                                   manifestResource.Bindings,
                                   manifestResource.Env),
                var rt when rt.Contains("container", StringComparison.OrdinalIgnoreCase)
                    => new Container(Namespace,
                                     Name,
                                     resourceName,
                                     Dockerfile: manifestResource switch
                                     {
                                         var r when !string.IsNullOrEmpty(r.Image) => new Dockerfile(r.Image),
                                         var r when r.Build != null => CreateDockerfile(r, AppHostPath, resourceName),
                                         _ => throw new ArgumentException(nameof(resourceName)),
                                     },
                                     manifestResource.Bindings,
                                     manifestResource.Env),
                var rt when rt.Contains("parameter", StringComparison.OrdinalIgnoreCase)
                    => new Parameter(Namespace,
                                     Name,
                                     resourceName,
                                     manifestResource.Value,
                                     manifestResource.Inputs),
                var rt when rt.Contains("value", StringComparison.OrdinalIgnoreCase) => throw new NotImplementedException(resourceName),
                _ => throw new ArgumentException(resourceName),
            };

        Dockerfile CreateDockerfile(ManifestResource r, string appHostPath, string resourceName)
        {
            var context = Path.Combine(appHostPath, r.Build.Context) ?? ".";
            var path = Path.Combine(appHostPath, r.Build.Dockerfile.Replace("/", "\\") ?? "Dockerfile");

            return new Dockerfile(resourceName,
                Context: context,
                Path: path,
                ShouldBuildWithDocker: true);
        }
    }

    public async Task<ResourceOperationResult> CheckNamespace(k8s.Kubernetes k8s, bool shouldCreateIfNotExists = true)
    {
        try
        {
            await k8s.ReadNamespaceAsync(Namespace);
            return ResourceOperationResult.Exists;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!shouldCreateIfNotExists)
            {
                return ResourceOperationResult.Missing;
            }

            await k8s.CreateNamespaceAsync(Defaults.V1Namespace(Namespace, Name));
            return ResourceOperationResult.Created;
        }
    }
}
