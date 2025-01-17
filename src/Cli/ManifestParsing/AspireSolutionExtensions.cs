using a2k.Shared;
using a2k.Shared.Models;
using Spectre.Console;
using System.Text.Json;

namespace a2k.Cli.ManifestParsing;

public static class AspireSolutionExtensions
{
    public static void CreateManifestIfNotExists(this AspireSolution aspireSolution)
    {
        if (!File.Exists(aspireSolution.ManifestPath))
        {
            AnsiConsole.MarkupLine($"[yellow]manifest.json file not found at {aspireSolution.ManifestPath}, creating...[/]");
            Shell.Run("dotnet run --publisher manifest --output-path manifest.json");
            AnsiConsole.MarkupLine("[green]manifest.json file created![/]");
        }
    }

    public static async Task ReadManifest(this AspireSolution aspireSolution)
    {
        aspireSolution.CreateManifestIfNotExists();

        AnsiConsole.MarkupLine($"[yellow]Loading manifest from: {aspireSolution.ManifestPath}[/]");
        var json = await File.ReadAllTextAsync(aspireSolution.ManifestPath);

        var manifest = JsonSerializer.Deserialize<AspireManifest>(json, Defaults.JsonSerializerOptions);
        if (manifest == null)
        {
            throw new ArgumentNullException(nameof(manifest), "Could not read AppHost's manifest.json!");
        }

        foreach (var (resourceName, resource) in manifest.Resources)
        {
            var resourceType = resource.MapAspireResourceType();
            if (resourceType == AspireResourceType.Project)
            {
                var csProjPath = Path.GetFullPath(Path.Combine(aspireSolution.AppHostPath, resource.Path));
                var projectName = Path.GetDirectoryName(csProjPath) ?? resourceName;

                var project = new AspireProject(resourceName, csProjPath)
                {
                    Dockerfile = new Dockerfile($"{aspireSolution.Name.ToLowerInvariant()}-{projectName.ToLowerInvariant()}", "latest")
                };

                aspireSolution.Resources.Add(project);
            }
            else if (resourceType == AspireResourceType.Container)
            {
                var container = new AspireContainer(resourceName)
                {
                    Dockerfile = resource switch
                    {
                        var r when !string.IsNullOrEmpty(r.Image) => new Dockerfile(r.Image),
                        var r when r.Build != null => r.CreateDockerfile(aspireSolution.AppHostPath, resourceName),
                        _ => throw new ArgumentException(nameof(resourceName)),
                    }
                };

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
            ShouldBuildWithDocker: false);
    }
}
