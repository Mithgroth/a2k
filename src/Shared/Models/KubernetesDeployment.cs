using k8s;
using k8s.Models;
using Spectre.Console;

namespace a2k.Shared.Models;

public class KubernetesDeployment(AspireSolution AspireSolution)
{
    private readonly Kubernetes k8s = new(KubernetesClientConfiguration.BuildConfigFromConfigFile());
    private Dictionary<string, string> CommonLabels { get; set; } = new Dictionary<string, string>
        {
            { "app.kubernetes.io/name", AspireSolution.Name },
            { "app.kubernetes.io/managed-by", "a2k" }
        };

    public async Task<ResourceOperationResult> CheckNamespace(bool shouldCreateIfNotExists = true)
    {
        try
        {
            await k8s.ReadNamespaceAsync(AspireSolution.Namespace);
            return ResourceOperationResult.Exists;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!shouldCreateIfNotExists)
            {
                return ResourceOperationResult.Missing;
            }

            var namespaceObj = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = AspireSolution.Namespace,
                    Labels = CommonLabels,
                }
            };

            await k8s.CreateNamespaceAsync(namespaceObj);
            return ResourceOperationResult.Created;
        }
    }

    /// <summary>
    /// Deploy the resources found in the Aspire manifest. 
    /// In a real scenario, you’d create or update Deployments, Services, Secrets, etc.
    /// </summary>
    public async Task Deploy()
    {
        foreach (var resource in AspireSolution.Resources)
        {
            switch (resource)
            {
                case AspireContainer container when resource.ResourceType == AspireResourceType.Container:
                    await DeployContainerResource(container);
                    break;

                case AspireProject project when resource.ResourceType == AspireResourceType.Project:
                    await DeployProjectResource(project);
                    break;

                case var parameter when resource.ResourceType == AspireResourceType.Parameter:
                    //await HandleParameterResource(resource.Name, resource, k8sNamespace);
                    break;

                default:
                    Console.WriteLine($"[WARN] Resource '{resource.ResourceName}' has unsupported type '{resource.ResourceType}'. Skipping.");
                    break;
            };
        }
    }

    private async Task DeployContainerResource(AspireContainer container)
    {
        AnsiConsole.MarkupLine($"[bold gray]Deploying container resource: {container.ResourceName}[/]");

        if (container.Dockerfile?.ShouldBuildWithDocker == true)
        {
            var buildCommand = $"docker build -t {container.Dockerfile.Name}";
            if (!string.IsNullOrEmpty(container.Dockerfile?.Path))
            {
                buildCommand += $" -f {container.Dockerfile.Path}";
            }

            if (!string.IsNullOrEmpty(container.Dockerfile?.Context))
            {
                buildCommand += $" {container.Dockerfile.Context}";
            }

            Shell.Run(buildCommand);
        }

        //var deployment = CreateBasicDeployment(
        //    container.Name,
        //    container.Env,
        //    container.Bindings,
        //    $"{container.Dockerfile.Name}:{container.Dockerfile.Tag}",
        //    container.Dockerfile?.ShouldBuildWithDocker == true ? "Never" : "IfNotPresent"
        //);

        //var deployment = CreateBasicDeployment(container);
        var deployment = container.ToKubernetesDeployment();
        var service = container.ToKubernetesService();

        //var service = CreateBasicService(
        //    container.Name,
        //    container.Bindings,
        //    AspireSolution.Name
        //);

        // Create or replace (for brevity, we’ll just create)
        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, AspireSolution.Namespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Deployment {AspireSolution.Name} already exists or creation failed. You may want to implement patch/replace logic.[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, AspireSolution.Namespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Service {AspireSolution.Name} already exists or creation failed. You may want to implement patch/replace logic.[/]");
        }
    }

    private async Task DeployProjectResource(AspireProject project)
    {
        project.PublishContainer();

        var deployment = project.ToKubernetesDeployment();
        var service = project.ToKubernetesService();

        //var deployment = CreateBasicDeployment(
        //    project.Name,
        //    project.Env,
        //    project.Bindings,
        //    $"{project.Dockerfile.Name}:{project.Dockerfile.Tag}",
        //    project.Dockerfile?.ShouldBuildWithDocker == true ? "Never" : "IfNotPresent"
        //);

        //var service = CreateBasicService(
        //    project.Name,
        //    project.Bindings,
        //    AspireSolution.Name
        //);

        // Create or replace
        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, AspireSolution.Namespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Deployment {AspireSolution.Namespace} already exists or creation failed.[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, AspireSolution.Namespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Service {AspireSolution.Namespace} already exists or creation failed.[/]");
        }
    }

    private async Task HandleParameterResource(string name, ManifestResource resource, string k8sNamespace)
    {
        AnsiConsole.MarkupLine($"[bold gray]Handling parameter resource: {name}[/]");

        // If the parameter is secret (e.g., password), create a Secret
        if (resource.Inputs != null && resource.Inputs.TryGetValue("value", out var paramInput) && paramInput.Secret)
        {
            // The actual password might be in resource.Value or resource.Value might come from the user.
            // If it doesn’t exist yet, we might need to generate. For now, assume it’s already populated.
            var secretValue = resource.Value;
            var secretName = name.Replace("-password", "-secret"); // example naming logic

            var secret = new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta
                {
                    Name = secretName,
                    NamespaceProperty = k8sNamespace
                },
                Data = new Dictionary<string, byte[]>
                {
                    // Convert secretValue to Base64 or keep it as an ASCII
                    { "password", System.Text.Encoding.UTF8.GetBytes(secretValue ?? "") }
                }
            };

            try
            {
                await k8s.CreateNamespacedSecretAsync(secret, k8sNamespace);
            }
            catch
            {
                AnsiConsole.MarkupLine($"[bold yellow]Secret {secretName} already exists or creation failed.[/]");
            }
        }
        else
        {
            // Maybe we store it in a ConfigMap if not secret
            AnsiConsole.MarkupLine($"[bold yellow]Parameter {name} is not marked secret, ignoring or store in ConfigMap if desired.[/]");
        }
    }

    //private V1Deployment CreateBasicDeployment(AspireResource resource)
    //{
    //    // Figure out a port from the "bindings" if present
    //    // For a simple example, pick the first binding that has a targetPort.
    //    var port = 80; // default
    //    if (resource.Bindings != null)
    //    {
    //        foreach (var b in resource.Bindings.Values)
    //        {
    //            if (b.TargetPort.HasValue)
    //            {
    //                port = b.TargetPort.Value;
    //                break;
    //            }
    //        }
    //    }

    //    // Convert env dict to list
    //    var containerEnv = new List<V1EnvVar>();
    //    if (resource.Env != null)
    //    {
    //        foreach (var (key, value) in resource.Env)
    //        {
    //            containerEnv.Add(new V1EnvVar(key, value));
    //        }
    //    }

    //    var labels = new Dictionary<string, string>
    //    {
    //        { "app", resource.Name },
    //        { "app.kubernetes.io/name", AspireSolution.Name },
    //        { "app.kubernetes.io/managed-by", "a2k" },
    //        { "app.kubernetes.io/component", resource.Name }
    //    };

    //    // Build the K8s Deployment
    //    return new V1Deployment
    //    {
    //        ApiVersion = "apps/v1",
    //        Kind = "Deployment",
    //        Metadata = new V1ObjectMeta
    //        {
    //            Name = resource.Name,
    //            Labels = labels
    //        },
    //        Spec = new V1DeploymentSpec
    //        {
    //            Replicas = 1,
    //            Selector = new V1LabelSelector
    //            {
    //                MatchLabels = labels
    //            },
    //            Template = new V1PodTemplateSpec
    //            {
    //                Metadata = new V1ObjectMeta
    //                {
    //                    Labels = labels
    //                },
    //                Spec = new V1PodSpec
    //                {
    //                    Containers =
    //                    [
    //                        new() {
    //                            Name = resource.Name,
    //                            Image = $"{resource.Dockerfile.Name}:{resource.Dockerfile.Tag}",
    //                            ImagePullPolicy = resource.Dockerfile?.ShouldBuildWithDocker == true ? "Never" : "IfNotPresent",
    //                            Ports =
    //                            [
    //                                new(port)
    //                            ],
    //                            Env = containerEnv
    //                        }
    //                    ]
    //                }
    //            }
    //        }
    //    };
    //}

    //private V1Deployment CreateBasicDeployment(
    //    string name,
    //    Dictionary<string, string> envVars,
    //    Dictionary<string, ResourceBinding> bindings,
    //    string image,
    //    string imagePullPolicy = "Never")
    //{
    //    // Figure out a port from the "bindings" if present
    //    // For a simple example, pick the first binding that has a targetPort.
    //    var port = 80; // default
    //    if (bindings != null)
    //    {
    //        foreach (var b in bindings.Values)
    //        {
    //            if (b.TargetPort.HasValue)
    //            {
    //                port = b.TargetPort.Value;
    //                break;
    //            }
    //        }
    //    }

    //    // Convert env dict to list
    //    var containerEnv = new List<V1EnvVar>();
    //    if (envVars != null)
    //    {
    //        foreach (var (key, value) in envVars)
    //        {
    //            containerEnv.Add(new V1EnvVar(key, value));
    //        }
    //    }

    //    var labels = new Dictionary<string, string>
    //    {
    //        { "app", name },
    //        { "app.kubernetes.io/name", AspireSolution.Name },
    //        { "app.kubernetes.io/managed-by", "a2k" },
    //        { "app.kubernetes.io/component", name }
    //    };

    //    // Build the K8s Deployment
    //    return new V1Deployment
    //    {
    //        ApiVersion = "apps/v1",
    //        Kind = "Deployment",
    //        Metadata = new V1ObjectMeta
    //        {
    //            Name = name,
    //            Labels = labels
    //        },
    //        Spec = new V1DeploymentSpec
    //        {
    //            Replicas = 1,
    //            Selector = new V1LabelSelector
    //            {
    //                MatchLabels = labels
    //            },
    //            Template = new V1PodTemplateSpec
    //            {
    //                Metadata = new V1ObjectMeta
    //                {
    //                    Labels = labels
    //                },
    //                Spec = new V1PodSpec
    //                {
    //                    Containers =
    //                    [
    //                        new() {
    //                            Name = name,
    //                            Image = image,
    //                            ImagePullPolicy = imagePullPolicy,
    //                            Ports =
    //                            [
    //                                new(port)
    //                            ],
    //                            Env = containerEnv
    //                        }
    //                    ]
    //                }
    //            }
    //        }
    //    };
    //}

    //private V1Service CreateBasicService(
    //    string name,
    //    Dictionary<string, ResourceBinding> bindings,
    //    string applicationName)
    //{
    //    // Identify at least one port to expose
    //    var port = 80; // default
    //    if (bindings != null)
    //    {
    //        foreach (var b in bindings.Values)
    //        {
    //            if (b.TargetPort.HasValue)
    //            {
    //                port = b.TargetPort.Value;
    //                break;
    //            }
    //        }
    //    }

    //    var labels = new Dictionary<string, string>
    //    {
    //        { "app", name },
    //        { "app.kubernetes.io/name", applicationName },
    //        { "app.kubernetes.io/managed-by", "a2k" },
    //        { "app.kubernetes.io/component", name }
    //    };

    //    return new V1Service
    //    {
    //        ApiVersion = "v1",
    //        Kind = "Service",
    //        Metadata = new V1ObjectMeta
    //        {
    //            Name = $"{name}-service",
    //            Labels = labels
    //        },
    //        Spec = new V1ServiceSpec
    //        {
    //            Selector = labels,
    //            Ports =
    //            [
    //                new()
    //                {
    //                    Port = port,
    //                    TargetPort = port
    //                }
    //            ]
    //        }
    //    };
    //}
}
