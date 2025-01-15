using a2k.Cli.Models;

namespace a2k.Cli.Services;

public static class DockerService
{
    public static List<(string Name, string Context, string Dockerfile, bool IsProject)> FindImagesToBuild(AspireManifest manifest, string appHostPath)
    {
        var imagesToBuild = new List<(string Name, string Context, string Dockerfile, bool IsProject)>();

        foreach (var (resourceName, resource) in manifest.Resources)
        {
            if (resource.Build != null)
            {
                // Resources with a `build` section
                var context = Path.Combine(appHostPath, resource.Build.Context) ?? ".";
                var dockerfile = Path.Combine(appHostPath, resource.Build.Dockerfile.Replace("/", "\\") ?? "Dockerfile");
                imagesToBuild.Add((resourceName, context, dockerfile, false));
            }
            else if (resource.ResourceType.Equals("project.v0", StringComparison.OrdinalIgnoreCase))
            {
                var projectPath = Path.GetFullPath(Path.Combine(appHostPath, resource.Path));

                // Resources of type `project.v0`
                //var projectPath = resource.Path ?? throw new Exception($"Resource {resourceName} is missing the path to its project file.");
                var context = Path.GetDirectoryName(projectPath) ?? ".";

                // No Dockerfile specified; we'll generate one dynamically later if needed
                var dockerfile = Path.Combine(appHostPath, context, "Dockerfile");
                imagesToBuild.Add((resourceName, context, dockerfile, true));
            }
        }

        return imagesToBuild;
    }
}
