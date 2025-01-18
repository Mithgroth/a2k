using a2k.Shared.Models.Kubernetes;
using k8s;
using Spectre.Console;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a .NET project in Aspire environment
/// </summary>
public record Project(string SolutionName,
                            string ResourceName,
                            string CsProjPath,
                            Dockerfile? Dockerfile,
                            Dictionary<string, ResourceBinding> Bindings,
                            Dictionary<string, string> Env)
    : Resource(SolutionName, ResourceName, Dockerfile, Bindings, Env, AspireResourceType.Project)
{
    public void PublishContainer()
    {
        AnsiConsole.MarkupLine($"[bold gray]Building .NET project {ResourceName}...[/]");
        Shell.Run($"dotnet publish {Directory.GetParent(CsProjPath)} -c Release --verbosity quiet --os linux /t:PublishContainer /p:ContainerRepository={Dockerfile.Name.Replace(":latest", "")}");
        AnsiConsole.MarkupLine($"[bold green]Published Docker image for {ResourceName} as {Dockerfile.Name.Replace(":latest", "")}[/]");
    }

    public override async Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s)
    {
        PublishContainer();

        var deployment = ToKubernetesDeployment();
        var service = ToKubernetesService();

        // Create or replace
        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, "AspireSolution.Namespace");
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Deployment for {ResourceName} already exists or creation failed.[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, "AspireSolution.Namespace");
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Service for {ResourceName} already exists or creation failed.[/]");
        }

        return ResourceOperationResult.Created;
    }
}