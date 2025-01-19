using a2k.Shared.Models.Kubernetes;
using k8s.Models;
using Spectre.Console;

namespace a2k.Shared.Models.Aspire;

public abstract record Resource(string SolutionName,
                                string ResourceName,
                                Dictionary<string, ResourceBinding>? Bindings,
                                Dictionary<string, string>? Env,
                                AspireResourceType ResourceType = AspireResourceType.Unknown)
{
    public Dockerfile? Dockerfile { get; set; }

    protected Resource(string solutionName,
                    string resourceName,
                    Dockerfile? dockerfile,
                    Dictionary<string, ResourceBinding>? bindings,
                    Dictionary<string, string>? env,
                    AspireResourceType resourceType)
        : this(solutionName, resourceName, bindings, env, resourceType)
    {
        Dockerfile = dockerfile;
    }

    protected void CleanupOldImages()
    {
        if (Dockerfile?.SHA256 == null || !Dockerfile.ShouldBuildWithDocker)
        {
            return;
        }

        // Find all images with the same name but different SHA256
        var images = Shell.Run($"docker images {Dockerfile.Name} --quiet --no-trunc")
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        foreach (var image in images)
        {
            var sha = image.Replace("sha256:", "").Trim('\'', '"').Trim();
            if (sha != Dockerfile.SHA256)
            {
                try
                {
                    Shell.Run($"docker rmi {sha} --force");
                    AnsiConsole.MarkupLine($"[gray]Removed old image {sha} for {ResourceName}[/]");
                }
                catch
                {
                    // Ignore errors during cleanup
                    AnsiConsole.MarkupLine($"[yellow]Could not remove old image {sha} for {ResourceName}[/]");
                }
            }
        }
    }

    public V1Deployment ToKubernetesDeployment()
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

        var resource = Defaults.V1Deployment(SolutionName, ResourceName);
        resource.Spec = new V1DeploymentSpec
        {
            Replicas = 1,
            Selector = Defaults.V1LabelSelector(resource.Metadata.Labels),
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = Defaults.Labels(SolutionName, ResourceName),
                    Annotations = new Dictionary<string, string>
                    {
                        ["a2k.version"] = Dockerfile?.Tag
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

    public V1Service ToKubernetesService()
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

        var resource = Defaults.V1Service(SolutionName, ResourceName);
        resource.Spec = new V1ServiceSpec
        {
            Selector = Defaults.Labels(SolutionName, ResourceName),
            Ports = [new() { Port = port, TargetPort = port }]
        };

        return resource;
    }

    public abstract Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s);
}

public enum AspireResourceType
{
    Unknown,
    Project,
    Container,
    Parameter,
    Value
}
