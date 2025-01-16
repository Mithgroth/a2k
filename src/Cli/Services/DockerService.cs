using a2k.Cli.Models;

namespace a2k.Cli.Services;

public static class DockerService
{
    public static List<(string ResourceName, string Context, string Dockerfile, bool IsProject, string ImageName)> FindImagesToBuild(AspireManifest manifest, string appHostPath)
    {
        var imagesToBuild = new List<(string ResourceName, string Context, string Dockerfile, bool IsProject, string ImageName)>();

        foreach (var (resourceName, resource) in manifest.Resources)
        {
            if (resource.Build != null)
            {
                // Resources with a `build` section
                var context = Path.Combine(appHostPath, resource.Build.Context) ?? ".";
                var dockerfile = Path.Combine(appHostPath, resource.Build.Dockerfile.Replace("/", "\\") ?? "Dockerfile");
                imagesToBuild.Add((resourceName, context, dockerfile, false, $"{resourceName}:latest"));
            }
            else if (resource.ResourceType.Equals("project.v0", StringComparison.OrdinalIgnoreCase))
            {
                var projectPath = Path.GetFullPath(Path.Combine(appHostPath, resource.Path));
                var context = Path.GetDirectoryName(projectPath) ?? ".";
                var dockerfile = Path.Combine(appHostPath, context, "Dockerfile");
                var solutionName = Path.GetFileName(Directory.GetParent(appHostPath)?.FullName ?? "aspire-app")
                    .Replace(".sln", string.Empty);
                var imageName = $"{solutionName.ToLower()}-{resourceName}:latest";
                imagesToBuild.Add((resourceName, context, dockerfile, true, imageName));
            }
        }

        return imagesToBuild;
    }
}
