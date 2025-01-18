using a2k.Shared;
using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using Spectre.Console;
using System.Text.Json;

namespace a2k.Cli.ManifestParsing;

internal static class AspireSolutionExtensions
{
    internal static void CreateManifestIfNotExists(this Solution aspireSolution)
    {
        if (!File.Exists(aspireSolution.ManifestPath))
        {
            AnsiConsole.MarkupLine($"[yellow]manifest.json file not found at {aspireSolution.ManifestPath}, creating...[/]");
            Shell.Run("dotnet run --publisher manifest --output-path manifest.json");
            AnsiConsole.MarkupLine("[green]manifest.json file created![/]");
        }
    }

    internal static async Task ReadManifest(this Solution aspireSolution)
    {
        aspireSolution.CreateManifestIfNotExists();

        var path = new TextPath(aspireSolution.ManifestPath)
            .RootColor(Color.Wheat4)
            .SeparatorColor(Color.White)
            .StemColor(Color.Wheat4)
            .LeafColor(Color.Yellow);

        var panel = new Panel(path)
        {
            Header = new("Loading manifest from")
        };

        AnsiConsole.Write(panel);

        var json = await File.ReadAllTextAsync(aspireSolution.ManifestPath);

        var manifest = JsonSerializer.Deserialize<Manifest>(json, Defaults.JsonSerializerOptions);
        if (manifest == null)
        {
            throw new ArgumentNullException(nameof(manifest), "Could not read AppHost's manifest.json!");
        }

        aspireSolution.Manifest = manifest;

        foreach (var (resourceName, resource) in manifest.Resources)
        {
            var resourceType = resource.MapAspireResourceType();
            if (resourceType == AspireResourceType.Project)
            {
                var project = new Project(
                    aspireSolution.Name,
                    resourceName,
                    CsProjPath: Path.GetFullPath(Path.Combine(aspireSolution.AppHostPath, resource.Path)),
                    new Dockerfile($"{aspireSolution.Name.ToLowerInvariant()}-{resourceName.ToLowerInvariant()}", "latest"),
                    resource.Bindings,
                    resource.Env);

                aspireSolution.Resources.Add(project);
            }
            else if (resourceType == AspireResourceType.Container)
            {
                var container = new Container(
                    aspireSolution.Name,
                    resourceName,
                    Dockerfile: resource switch
                    {
                        var r when !string.IsNullOrEmpty(r.Image) => new Dockerfile(r.Image),
                        var r when r.Build != null => r.CreateDockerfile(aspireSolution.AppHostPath, resourceName),
                        _ => throw new ArgumentException(nameof(resourceName)),
                    },
                    resource.Bindings,
                    resource.Env);

                aspireSolution.Resources.Add(container);
            }
        }
    }

    private static AspireResourceType MapAspireResourceType(this ManifestResource resource)
    {
        return resource.ResourceType switch
        {
            var rt when rt.Contains("project", StringComparison.OrdinalIgnoreCase) => AspireResourceType.Project,
            var rt when rt.Contains("container", StringComparison.OrdinalIgnoreCase) => AspireResourceType.Container,
            var rt when rt.Contains("parameter", StringComparison.OrdinalIgnoreCase) => AspireResourceType.Parameter,
            var rt when rt.Contains("value", StringComparison.OrdinalIgnoreCase) => AspireResourceType.Value,
            _ => AspireResourceType.Unknown,
        };
    }

    private static Dockerfile CreateDockerfile(this ManifestResource r, string appHostPath, string resourceName)
    {
        var context = Path.Combine(appHostPath, r.Build.Context) ?? ".";
        var path = Path.Combine(appHostPath, r.Build.Dockerfile.Replace("/", "\\") ?? "Dockerfile");

        return new Dockerfile(resourceName,
            Context: context,
            Path: path,
            ShouldBuildWithDocker: true);
    }
}
