using a2k.Shared.Models.Kubernetes;
using k8s;
using Spectre.Console;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a container resource in .NET Aspire environment (redis, postgres, etc)
/// </summary>
public record Container(string Namespace,
                        string SolutionName,
                        string ResourceName,
                        Dockerfile? Dockerfile,
                        Dictionary<string, ResourceBinding> Bindings,
                        Dictionary<string, string> Env)
    : Resource(SolutionName, ResourceName, Dockerfile, Bindings, Env, AspireResourceType.Container)
{
    public override async Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s)
    {
        AnsiConsole.MarkupLine($"[bold gray]Deploying container resource: {ResourceName}[/]");

        if (Dockerfile?.ShouldBuildWithDocker == true)
        {
            var buildCommand = $"docker build -t {Dockerfile.Name}";
            if (!string.IsNullOrEmpty(Dockerfile?.Path))
            {
                buildCommand += $" -f {Dockerfile.Path}";
            }

            if (!string.IsNullOrEmpty(Dockerfile?.Context))
            {
                buildCommand += $" {Dockerfile.Context}";
            }
            Shell.Run(buildCommand);

            // Get the SHA256 of the built image
            var sha256 = Shell.Run($"docker inspect --format={{{{.Id}}}} {Dockerfile.Name}").Replace("sha256:", "").Trim();
            Dockerfile = Dockerfile.UpdateSHA256(sha256);
        }

        var deployment = ToKubernetesDeployment();
        var service = ToKubernetesService();

        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, Namespace);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error deploying {ResourceName}: {ex.Message}[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, Namespace);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error deploying {ResourceName} service: {ex.Message}[/]");
        }

        CleanupOldImages();
        return ResourceOperationResult.Created;
    }
}