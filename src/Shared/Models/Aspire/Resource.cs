using a2k.Shared.Models.Kubernetes;
using k8s.Models;

namespace a2k.Shared.Models.Aspire;

public abstract record Resource(Solution Solution,
                                string ResourceName,
                                Dictionary<string, ResourceBinding>? Bindings,
                                Dictionary<string, string>? Env,
                                AspireResourceType ResourceType = AspireResourceType.Unknown)
{
    public Dockerfile? Dockerfile { get; set; }

    protected Resource(Solution solution,
                       string resourceName,
                       Dockerfile? dockerfile,
                       Dictionary<string, ResourceBinding>? bindings,
                       Dictionary<string, string>? env,
                       AspireResourceType resourceType)
        : this(solution, resourceName, bindings, env, resourceType)
    {
        Dockerfile = dockerfile;
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

        var resource = Defaults.V1Deployment(ResourceName, Solution.Env, Solution.Tag);
        resource.Spec = new V1DeploymentSpec
        {
            Replicas = 1,
            Selector = Defaults.V1LabelSelector(resource.Metadata.Labels),
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

        var resource = Defaults.V1Service(ResourceName, Solution.Env, Solution.Tag);
        resource.Spec = new V1ServiceSpec
        {
            Selector = Defaults.Labels(Solution.Env, Solution.Tag),
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
