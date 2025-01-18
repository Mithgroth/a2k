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
        }

        var deployment = ToKubernetesDeployment();
        var service = ToKubernetesService();

        // Create or replace (for brevity, we’ll just create)
        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, Namespace);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold yellow]Deployment for {ResourceName} already exists or creation failed. You may want to implement patch/replace logic.[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, Namespace);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold yellow]Service for {ResourceName} already exists or creation failed. You may want to implement patch/replace logic.[/]");
        }

        return ResourceOperationResult.Created;
    }
}