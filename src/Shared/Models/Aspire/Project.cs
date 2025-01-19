using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Models;
using Spectre.Console;
using System.Text.Json;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a .NET project in Aspire environment
/// </summary>
public record Project(string Namespace,
                      string SolutionName,
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

        // Get the SHA256 of the built image
        var sha256 = Shell.Run($"docker inspect --format={{{{.Id}}}} {Dockerfile.Name}").Replace("sha256:", "").Trim();
        Dockerfile = Dockerfile.UpdateSHA256(sha256);
    }

    public override async Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s)
    {
        PublishContainer();

        var deployment = ToKubernetesDeployment();
        var service = ToKubernetesService();

        try
        {
            try
            {
                // Try to get existing deployment
                await k8s.ReadNamespacedDeploymentAsync(deployment.Metadata.Name, Namespace);
                
                // Create patch using K8s models
                var deploymentPatch = new V1Deployment
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = deployment.Metadata.Name,
                    },
                    Spec = new V1DeploymentSpec
                    {
                        Selector = deployment.Spec.Selector,
                        Template = new V1PodTemplateSpec
                        {
                            Metadata = new V1ObjectMeta
                            {
                                Labels = deployment.Spec.Template.Metadata.Labels,
                                Annotations = new Dictionary<string, string>
                                {
                                    ["a2k.version"] = Dockerfile?.Tag,
                                    ["kubectl.kubernetes.io/restartedAt"] = DateTime.UtcNow.ToString("o")
                                }
                            },
                            Spec = new V1PodSpec
                            {
                                Containers = new[]
                                {
                                    new V1Container
                                    {
                                        Name = ResourceName,
                                        Image = Dockerfile?.FullImageName,
                                        ImagePullPolicy = Dockerfile?.ShouldBuildWithDocker == true ? "Never" : "IfNotPresent",
                                        Ports = deployment.Spec.Template.Spec.Containers[0].Ports
                                    }
                                }
                            }
                        }
                    }
                };

                var patchJson = JsonSerializer.Serialize(deploymentPatch, Defaults.JsonSerializerOptions);
                
                // If exists, patch it with strategic merge
                await k8s.PatchNamespacedDeploymentAsync(
                    new V1Patch(patchJson, V1Patch.PatchType.StrategicMergePatch),
                    deployment.Metadata.Name,
                    Namespace);
                
                AnsiConsole.MarkupLine($"[bold blue]Updated existing deployment for {ResourceName}[/]");
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // If doesn't exist, create it
                await k8s.CreateNamespacedDeploymentAsync(deployment, Namespace);
                AnsiConsole.MarkupLine($"[bold green]Created new deployment for {ResourceName}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error deploying {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]");
        }

        try
        {
            try
            {
                // Try to get existing service
                await k8s.ReadNamespacedServiceAsync(service.Metadata.Name, Namespace);
                
                // Create patch using K8s models
                var servicePatch = new V1Service
                {
                    Metadata = new V1ObjectMeta
                    {
                        Name = service.Metadata.Name,
                        Annotations = new Dictionary<string, string>
                        {
                            ["a2k.version"] = Dockerfile?.Tag
                        }
                    },
                    Spec = new V1ServiceSpec
                    {
                        Ports = new[] 
                        { 
                            new V1ServicePort
                            {
                                Port = service.Spec.Ports[0].Port,
                                TargetPort = service.Spec.Ports[0].TargetPort
                            }
                        },
                        Selector = service.Spec.Selector
                    }
                };

                var servicePatchJson = JsonSerializer.Serialize(servicePatch, Defaults.JsonSerializerOptions);
                
                // If exists, patch it with strategic merge
                await k8s.PatchNamespacedServiceAsync(
                    new V1Patch(servicePatchJson, V1Patch.PatchType.StrategicMergePatch),
                    service.Metadata.Name,
                    Namespace);
                
                AnsiConsole.MarkupLine($"[bold blue]Updated existing service for {ResourceName}[/]");
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // If doesn't exist, create it
                await k8s.CreateNamespacedServiceAsync(service, Namespace);
                AnsiConsole.MarkupLine($"[bold green]Created new service for {ResourceName}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Error deploying {ResourceName} service: {ex.Message}[/]");
        }

        CleanupOldImages();
        return ResourceOperationResult.Created;
    }
}