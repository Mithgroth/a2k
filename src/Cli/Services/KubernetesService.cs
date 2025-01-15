﻿using a2k.Cli.Models;
using k8s;
using k8s.Models;

namespace a2k.Cli.Services;

public class KubernetesService
{
    private readonly Kubernetes _client;

    public KubernetesService()
    {
        // By default, uses ~/.kube/config
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        _client = new Kubernetes(config);
    }

    /// <summary>
    /// Deploy the resources found in the Aspire manifest. 
    /// In a real scenario, you’d create or update Deployments, Services, Secrets, etc.
    /// </summary>
    public async Task DeployManifestAsync(AspireManifest manifest, string k8sNamespace = "default")
    {
        // For each resource in the manifest, handle according to its type
        foreach (var (resourceName, resource) in manifest.Resources)
        {
            var resourceType = resource.ResourceType ?? string.Empty;

            if (resourceType.Equals("container.v1", StringComparison.OrdinalIgnoreCase))
            {
                await DeployContainerResource(resourceName, resource, k8sNamespace);
            }
            else if (resourceType.Equals("project.v0", StringComparison.OrdinalIgnoreCase))
            {
                await DeployProjectResource(resourceName, resource, k8sNamespace);
            }
            else if (resourceType.Equals("parameter.v0", StringComparison.OrdinalIgnoreCase))
            {
                // Example: create a Secret for the parameter if secret = true
                await HandleParameterResource(resourceName, resource, k8sNamespace);
            }
            else
            {
                Console.WriteLine($"[WARN] Resource '{resourceName}' has unsupported type '{resourceType}'. Skipping.");
            }
        }
    }

    private async Task DeployContainerResource(string name, AspireResource resource, string k8sNamespace)
    {
        Console.WriteLine($"[INFO] Deploying container resource: {name}");

        // Example: Create Deployment + Service
        var deployment = CreateBasicDeployment(
            name,
            resource.Env,
            resource.Bindings,
            image: "nginx:latest" // Or some logic to retrieve from `resource.build` info
        );

        var service = CreateBasicService(
            name,
            resource.Bindings
        );

        // Create or replace (for brevity, we’ll just create)
        try
        {
            await _client.CreateNamespacedDeploymentAsync(deployment, k8sNamespace);
        }
        catch
        {
            Console.WriteLine($"[WARN] Deployment {name} already exists or creation failed. You may want to implement patch/replace logic.");
        }

        try
        {
            await _client.CreateNamespacedServiceAsync(service, k8sNamespace);
        }
        catch
        {
            Console.WriteLine($"[WARN] Service {name} already exists or creation failed. You may want to implement patch/replace logic.");
        }
    }

    private async Task DeployProjectResource(string name, AspireResource resource, string k8sNamespace)
    {
        Console.WriteLine($"[INFO] Deploying project resource: {name}");

        // Possibly build an image from the project, or retrieve an image name from CI, etc.
        // For demonstration, we’ll just assume we have a final container image like "myregistry.com/{name}:latest"
        var imageName = $"{name}:latest";

        var deployment = CreateBasicDeployment(
            name,
            resource.Env,
            resource.Bindings,
            imageName
        );

        var service = CreateBasicService(
            name,
            resource.Bindings
        );

        // Create or replace
        try
        {
            await _client.CreateNamespacedDeploymentAsync(deployment, k8sNamespace);
        }
        catch
        {
            Console.WriteLine($"[WARN] Deployment {name} already exists or creation failed.");
        }

        try
        {
            await _client.CreateNamespacedServiceAsync(service, k8sNamespace);
        }
        catch
        {
            Console.WriteLine($"[WARN] Service {name} already exists or creation failed.");
        }
    }

    private async Task HandleParameterResource(string name, AspireResource resource, string k8sNamespace)
    {
        Console.WriteLine($"[INFO] Handling parameter resource: {name}");

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
                await _client.CreateNamespacedSecretAsync(secret, k8sNamespace);
            }
            catch
            {
                Console.WriteLine($"[WARN] Secret {secretName} already exists or creation failed.");
            }
        }
        else
        {
            // Maybe we store it in a ConfigMap if not secret
            Console.WriteLine($"[INFO] Parameter {name} is not marked secret, ignoring or store in ConfigMap if desired.");
        }
    }

    private V1Deployment CreateBasicDeployment(
        string name,
        Dictionary<string, string> envVars,
        Dictionary<string, ResourceBinding> bindings,
        string image)
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

        // Build the K8s Deployment
        return new V1Deployment
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = new V1ObjectMeta
            {
                Name = name
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        { "app", name }
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = new Dictionary<string, string>
                        {
                            { "app", name }
                        }
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

    private V1Service CreateBasicService(string name, Dictionary<string, ResourceBinding> bindings)
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

        return new V1Service
        {
            ApiVersion = "v1",
            Kind = "Service",
            Metadata = new V1ObjectMeta
            {
                Name = $"{name}-service"
            },
            Spec = new V1ServiceSpec
            {
                Selector = new Dictionary<string, string>
                {
                    { "app", name }
                },
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
