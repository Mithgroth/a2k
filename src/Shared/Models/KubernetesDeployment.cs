using k8s;
using k8s.Models;
using Spectre.Console;

namespace a2k.Shared.Models;

public class KubernetesDeployment
{
    private readonly Kubernetes k8s = new(KubernetesClientConfiguration.BuildConfigFromConfigFile());

    public string Namespace { get; set; }
    public Dictionary<string, string> CommonLabels { get; set; }

    public KubernetesDeployment(AspireSolution aspireSolution)
    {
        Namespace = aspireSolution.Namespace;
        CommonLabels = new Dictionary<string, string>
        {
            { "app.kubernetes.io/name", aspireSolution.Name },
            { "app.kubernetes.io/managed-by", "a2k" }
        };
    }

    public async Task<ResourceOperationResult> CheckNamespace(bool shouldCreateIfNotExists = true)
    {
        try
        {
            await k8s.ReadNamespaceAsync(Namespace);
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
                    Name = Namespace,
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
    public async Task Deploy(AspireSolution aspireSolution, string k8sNamespace = "default")
    {
        await CheckNamespace();

<<<<<<<< HEAD:src/Shared/Models/KubernetesDeployment.cs
        foreach (var resource in aspireSolution.Resources)
========
        try
>>>>>>>> 20efb2742eab0f4fad651263a70d79deb1011054:src/Cli/KubernetesService.cs
        {
            switch (resource)
            {
<<<<<<<< HEAD:src/Shared/Models/KubernetesDeployment.cs
                case AspireContainer container when resource.Type == AspireResourceType.Container:
                    await DeployContainerResource(aspireSolution, container);
                    break;

                case AspireProject project when resource.Type == AspireResourceType.Project:
                    //await DeployProjectResource(resource.Name, resource, k8sNamespace, aspireSolution.Name);
                    break;

                case var parameter when resource.Type == AspireResourceType.Parameter:
                    //await HandleParameterResource(resource.Name, resource, k8sNamespace);
                    break;

                default:
                    Console.WriteLine($"[WARN] Resource '{resource.Name}' has unsupported type '{resource.Type}'. Skipping.");
                    break;
            }
========
                Metadata = new V1ObjectMeta
                {
                    Name = k8sNamespace,
                    Labels = commonLabels
                }
            };

            await _client.CoreV1.CreateNamespaceAsync(namespaceObj);
            Console.WriteLine($"[INFO] Created namespace {k8sNamespace}");
>>>>>>>> 20efb2742eab0f4fad651263a70d79deb1011054:src/Cli/KubernetesService.cs
        }

    }

<<<<<<<< HEAD:src/Shared/Models/KubernetesDeployment.cs
    private async Task DeployContainerResource(AspireSolution aspireSolution, AspireContainer resource)
========
    private async Task DeployContainerResource(string name, ManifestResource resource, string k8sNamespace, string applicationName)
>>>>>>>> 20efb2742eab0f4fad651263a70d79deb1011054:src/Cli/KubernetesService.cs
    {
        AnsiConsole.MarkupLine($"[bold gray]Deploying container resource: {resource.Name}[/]");

        var imageName = $"{resource.Name}:latest";

<<<<<<<< HEAD:src/Shared/Models/KubernetesDeployment.cs
========
        var imageName = _resourceToImageMap.TryGetValue(name, out var image) ? image : $"{name}:latest";

>>>>>>>> 20efb2742eab0f4fad651263a70d79deb1011054:src/Cli/KubernetesService.cs
        var deployment = CreateBasicDeployment(
            resource.Name,
            resource.Env,
            resource.Bindings,
            imageName,
            aspireSolution.Name
        );

        var service = CreateBasicService(
            resource.Name,
            resource.Bindings,
            aspireSolution.Name
        );

        // Create or replace (for brevity, we’ll just create)
        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, aspireSolution.Namespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Deployment {aspireSolution.Name} already exists or creation failed. You may want to implement patch/replace logic.[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, aspireSolution.Namespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Service {aspireSolution.Name} already exists or creation failed. You may want to implement patch/replace logic.[/]");
        }
    }

    private async Task DeployProjectResource(string name, ManifestResource resource, string k8sNamespace, string applicationName)
    {
        AnsiConsole.MarkupLine($"[bold gray]Deploying project resource: {name}[/]");

        var imageName = $"{name}:latest";

        var deployment = CreateBasicDeployment(
            name,
            resource.Env,
            resource.Bindings,
            imageName,
            applicationName
        );

        var service = CreateBasicService(
            name,
            resource.Bindings,
            applicationName
        );

        // Create or replace
        try
        {
            await k8s.CreateNamespacedDeploymentAsync(deployment, k8sNamespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Deployment {name} already exists or creation failed.[/]");
        }

        try
        {
            await k8s.CreateNamespacedServiceAsync(service, k8sNamespace);
        }
        catch
        {
            AnsiConsole.MarkupLine($"[bold yellow]Service {name} already exists or creation failed.[/]");
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

    private V1Deployment CreateBasicDeployment(
        string name,
        Dictionary<string, string> envVars,
        Dictionary<string, ResourceBinding> bindings,
        string image,
        string applicationName)
    {
        // Figure out a port from the "bindings" if present
        // For a simple example, pick the first binding that has a targetPort.
        var port = 80; // default
        if (bindings != null)
        {
            foreach (var b in bindings.Values)
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
        if (envVars != null)
        {
            foreach (var (key, value) in envVars)
            {
                containerEnv.Add(new V1EnvVar(key, value));
            }
        }

        var labels = new Dictionary<string, string>
        {
            { "app", name },
            { "app.kubernetes.io/name", applicationName },
            { "app.kubernetes.io/managed-by", "a2k" },
            { "app.kubernetes.io/component", name }
        };

        // Build the K8s Deployment
        return new V1Deployment
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = new V1ObjectMeta
            {
                Name = name,
                Labels = labels
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = labels
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = labels
                    },
                    Spec = new V1PodSpec
                    {
                        Containers =
                        [
                            new() {
                                Name = name,
                                Image = image,
                                ImagePullPolicy = "Never",
                                Ports =
                                [
                                    new(port)
                                ],
                                Env = containerEnv
                            }
                        ]
                    }
                }
            }
        };
    }

    private V1Service CreateBasicService(
        string name,
        Dictionary<string, ResourceBinding> bindings,
        string applicationName)
    {
        // Identify at least one port to expose
        var port = 80; // default
        if (bindings != null)
        {
            foreach (var b in bindings.Values)
            {
                if (b.TargetPort.HasValue)
                {
                    port = b.TargetPort.Value;
                    break;
                }
            }
        }

        var labels = new Dictionary<string, string>
        {
            { "app", name },
            { "app.kubernetes.io/name", applicationName },
            { "app.kubernetes.io/managed-by", "a2k" },
            { "app.kubernetes.io/component", name }
        };

        return new V1Service
        {
            ApiVersion = "v1",
            Kind = "Service",
            Metadata = new V1ObjectMeta
            {
                Name = $"{name}-service",
                Labels = labels
            },
            Spec = new V1ServiceSpec
            {
                Selector = labels,
                Ports =
                [
                    new()
                    {
                        Port = port,
                        TargetPort = port
                    }
                ]
            }
        };
    }
}
