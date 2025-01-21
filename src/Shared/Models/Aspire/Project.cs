using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Autorest;
using Spectre.Console;
using System.Net;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a .NET project in Aspire environment
/// </summary>
public record Project(Solution Solution,
                      string ResourceName,
                      string CsProjPath,
                      bool UseVersioning,
                      Dockerfile Dockerfile,
                      Dictionary<string, ResourceBinding> Bindings,
                      Dictionary<string, string> Env)
    : Resource(Solution, ResourceName, Dockerfile, Bindings, Env, AspireResourceType.Project)
{
    public override async Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s)
    {
        PublishContainer();
        await DeployResource();
        await DeployService();

        if (Solution.UseVersioning == false)
        {
            Dockerfile.CleanupOldImages();
        }

        return ResourceOperationResult.Created;

        void PublishContainer()
        {
            AnsiConsole.MarkupLine($"[bold gray]Building .NET project {ResourceName}...[/]");

            var publishCommand = $"dotnet publish {Directory.GetParent(CsProjPath)} -c Release --verbosity quiet --os linux /t:PublishContainer /p:ContainerRepository={Dockerfile.Name}";
            if (Solution.UseVersioning)
            {
                publishCommand += $" /p:ContainerImageTags={Dockerfile.Tag}";
            }

            Shell.Run(publishCommand);
            AnsiConsole.MarkupLine($"[bold green]Published Docker image for {ResourceName} as {Dockerfile.FullImageName}[/]");

            var sha256 = Shell.Run($"docker inspect --format={{{{.Id}}}} {Dockerfile.FullImageName}").Replace("sha256:", "").Trim();
            Dockerfile = Dockerfile.UpdateSHA256(sha256);
        }

        // TODO: DeployResource and DeployService essential does the same thing.
        // However their Kubernetes types are not interfaced, so we cannot pattern them out.
        // Refactor later to DRY.

        async Task DeployResource()
        {
            var deployment = ToKubernetesDeployment();

            try
            {
                await k8s.ReadNamespacedDeploymentAsync(deployment.Metadata.Name, Solution.Name);

                if (Solution.UseVersioning)
                {
                    await k8s.ReplaceNamespacedDeploymentAsync(deployment, deployment.Metadata.Name, Solution.Name);

                    AnsiConsole.MarkupLine($"[bold blue]Pushed a new revision for {ResourceName} deployment[/]");
                }
                else
                {
                    await k8s.DeleteNamespacedDeploymentAsync(deployment.Metadata.Name, Solution.Name);
                    await k8s.CreateNamespacedDeploymentAsync(deployment, Solution.Name);

                    AnsiConsole.MarkupLine($"[bold blue]Replaced deployment for {ResourceName}[/]");
                }
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedDeploymentAsync(deployment, Solution.Name);
                AnsiConsole.MarkupLine($"[bold green]Created new deployment for {ResourceName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error deploying {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
        async Task DeployService()
        {
            var service = ToKubernetesService();

            try
            {
                await k8s.ReadNamespacedServiceAsync(service.Metadata.Name, Solution.Name);

                if (Solution.UseVersioning)
                {
                    await k8s.ReplaceNamespacedServiceAsync(service, service.Metadata.Name, Solution.Name);

                    AnsiConsole.MarkupLine($"[bold blue]Pushed a new revision for {ResourceName} service[/]");
                }
                else
                {
                    await k8s.DeleteNamespacedServiceAsync(service.Metadata.Name, Solution.Name);
                    await k8s.CreateNamespacedServiceAsync(service, Solution.Name);

                    AnsiConsole.MarkupLine($"[bold blue]Replaced service for {ResourceName}[/]");
                }
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedServiceAsync(service, Solution.Name);
                AnsiConsole.MarkupLine($"[bold green]Created new service for {ResourceName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error deploying {ResourceName} service: {ex.Message}[/]");
            }
        }
    }
}