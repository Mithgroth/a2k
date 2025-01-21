using k8s;
using k8s.Autorest;
using k8s.Models;
using Spectre.Console;
using System.Net;

namespace a2k.Shared.Models.Aspire;

public abstract record Resource(Solution Solution,
                                string ResourceName,
                                Dictionary<string, ResourceBinding>? Bindings,
                                Dictionary<string, string>? Env,
                                AspireResourceTypes ResourceType = AspireResourceTypes.Unknown)
{
    public Dockerfile? Dockerfile { get; set; }

    protected Resource(Solution solution,
                       string resourceName,
                       Dockerfile? dockerfile,
                       Dictionary<string, ResourceBinding>? bindings,
                       Dictionary<string, string>? env,
                       AspireResourceTypes resourceType)
        : this(solution, resourceName, bindings, env, resourceType)
    {
        Dockerfile = dockerfile;
    }

    public virtual V1Deployment ToKubernetesDeployment()
    {
        // Figure out a port from the "bindings" if present
        // For a simple example, pick the first binding that has a targetPort.
        var port = 80; // default
        if (Bindings != null)
        {
            foreach (var b in Bindings.Values)
            {
                if (b.TargetPort.HasValue)
                {
                    port = b.TargetPort.Value;
                    break;
                }
            }
        }

        // Convert env dict to list
        var containerEnv = new List<V1EnvVar>();
        if (Env != null)
        {
            foreach (var (key, value) in Env)
            {
                containerEnv.Add(new V1EnvVar(key, value));
            }
        }

        var resource = Defaults.V1Deployment(ResourceName, Solution.Env, Solution.Tag);
        resource.Spec = new V1DeploymentSpec
        {
            Replicas = 1,
            Selector = Defaults.V1LabelSelector(Defaults.SelectorLabels(Solution.Env)),
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = Defaults.Labels(Solution.Env, Solution.Tag),
                    Annotations = new Dictionary<string, string>
                    {
                        ["createdAt"] = DateTime.UtcNow.ToString("o")
                    }
                },
                Spec = new V1PodSpec
                {
                    Containers =
                        [
                            new()
                            {
                                Name = ResourceName,
                                Image = Dockerfile?.FullImageName,
                                ImagePullPolicy = Dockerfile?.ShouldBuildWithDocker == true ? "Never" : "IfNotPresent",
                                Ports =
                                [
                                    new(port)
                                ],
                                Env = containerEnv
                            }
                        ]
                }
            }
        };

        return resource;
    }

    public virtual V1Service ToKubernetesService()
    {
        // Identify at least one port to expose
        var port = 80; // default
        if (Bindings != null)
        {
            foreach (var b in Bindings.Values)
            {
                if (b.TargetPort.HasValue)
                {
                    port = b.TargetPort.Value;
                    break;
                }
            }
        }

        var resource = Defaults.V1Service(ResourceName, Solution.Env, Solution.Tag);
        resource.Spec = new V1ServiceSpec
        {
            Selector = Defaults.SelectorLabels(Solution.Env),
            Ports = [new() { Port = port, TargetPort = port }]
        };

        return resource;
    }

    public virtual async Task<Result> DeployResource(k8s.Kubernetes k8s)
    {

        var deployment = ToKubernetesDeployment();

        try
        {
            await k8s.ReadNamespacedDeploymentAsync(deployment.Metadata.Name, Solution.Name);

            if (Solution.UseVersioning)
            {
                await k8s.ReplaceNamespacedDeploymentAsync(deployment, deployment.Metadata.Name, Solution.Name);
                return new(ResourceOperationResult.Updated,
                [
                    new Markup($"[bold blue]Pushed a new revision for {ResourceName} deployment[/]"),
                ]);
            }
            else
            {
                await k8s.DeleteNamespacedDeploymentAsync(deployment.Metadata.Name, Solution.Name);
                await k8s.CreateNamespacedDeploymentAsync(deployment, Solution.Name);

                return new(ResourceOperationResult.Replaced,
                [
                    new Markup($"[bold blue]Replaced deployment for {ResourceName}[/]"),
                ]);
            }
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, Solution.Name);
            return new(ResourceOperationResult.Created,
            [
                new Markup($"[bold green]Created new deployment for {ResourceName}[/]"),
            ]);
        }
        catch (Exception ex)
        {
            return new(ResourceOperationResult.Failed,
            [
                new Markup($"[bold red]Error deploying {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]"),
            ]);
        }

        if (Solution.UseVersioning == false)
        {
            Dockerfile.CleanupOldImages();
        }
    }

    public virtual async Task<Result> DeployService(k8s.Kubernetes k8s)
    {
        var service = ToKubernetesService();

        try
        {
            await k8s.ReadNamespacedServiceAsync(service.Metadata.Name, Solution.Name);

            if (Solution.UseVersioning)
            {
                await k8s.ReplaceNamespacedServiceAsync(service, service.Metadata.Name, Solution.Name);
                return new(ResourceOperationResult.Updated,
                [
                    new Markup($"[bold blue]Pushed a new revision for {ResourceName} service[/]"),
                ]);
            }
            else
            {
                await k8s.DeleteNamespacedServiceAsync(service.Metadata.Name, Solution.Name);
                await k8s.CreateNamespacedServiceAsync(service, Solution.Name);
;
                return new(ResourceOperationResult.Replaced,
                [
                    new Markup($"[bold blue]Replaced service for {ResourceName}[/]"),
                ]);
            }
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await k8s.CreateNamespacedServiceAsync(service, Solution.Name);
            return new(ResourceOperationResult.Created,
            [
                new Markup($"[bold green]Created new service for {ResourceName}[/]"),
            ]);
        }
        catch (Exception ex)
        {
            return new(ResourceOperationResult.Failed,
            [
                new Markup($"[bold red] Error deploying {ResourceName} service: {ex.Message}[/]"),
            ]);
        }
    }
}

public enum AspireResourceTypes
{
    Unknown,
    Project,
    Container,
    Parameter,
    Value
}
