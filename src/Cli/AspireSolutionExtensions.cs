using a2k.Cli.Services;
using a2k.Shared;
using a2k.Shared.Models;
using System.Text.Json;

namespace a2k.Cli;

public static class AspireSolutionExtensions
{
    public static void CreateManifestIfNotExists(this AspireSolution aspireSolution)
    {
        if (!File.Exists(aspireSolution.ManifestPath))
        {
            Console.WriteLine($"manifest.json file not found at {aspireSolution.ManifestPath}, creating...");
            ShellService.RunCommand("run --publisher manifest --output-path manifest.json", "dotnet");
            Console.WriteLine($"manifest.json file created!");
        }
    }

    public static async Task ReadManifest(this AspireSolution aspireSolution)
    {
        Console.WriteLine($"[INFO] Loading manifest from: {aspireSolution.ManifestPath}");
        var json = await File.ReadAllTextAsync(aspireSolution.ManifestPath);

        var manifest = JsonSerializer.Deserialize<AspireManifest>(json, Defaults.JsonSerializerOptions);
        if (manifest == null)
        {
            Console.WriteLine($"Error: Failed to parse manifest at {aspireSolution.ManifestPath}");
            return;
        }

        foreach (var (resourceName, resource) in manifest.Resources)
        {
            var resourceType = MapAspireResourceType(resource);
            if (resourceType == AspireResourceType.Project)
            {
                var csProjPath = Path.GetFullPath(Path.Combine(aspireSolution.AppHostPath, resource.Path));
                var projectPath = Path.GetDirectoryName(csProjPath) ?? ".";
                //var dockerfile = Path.Combine(aspireSolution.AppHostPath, projectPath, "Dockerfile");
                //var imageName = $"{aspireSolution.Name.ToLower()}-{resourceName}:latest";

                var project = new AspireProject(resourceName, csProjPath)
                {
                    Dockerfile = new Dockerfile($"{aspireSolution.Name.ToLower()}-{resourceName}", "latest")
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
