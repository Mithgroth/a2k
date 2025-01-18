using k8s.Models;

namespace a2k.Shared.Models;

public abstract record AspireResource(string SolutionName,
                                      string ResourceName,
                                      Dockerfile? Dockerfile,
                                      Dictionary<string, ResourceBinding> Bindings,
                                      Dictionary<string, string> Env,
                                      AspireResourceType ResourceType = AspireResourceType.Unknown)
{
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

        // Build the K8s Deployment
        return new V1Deployment
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = new V1ObjectMeta
            {
                Name = ResourceName,
                Labels = Defaults.Labels(SolutionName, ResourceName),
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = Defaults.Labels(SolutionName, ResourceName),
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = Defaults.Labels(SolutionName, ResourceName),
                    },
                    Spec = new V1PodSpec
                    {
                        Containers =
                        [
                            new() {
                                Name = ResourceName,
                                Image = $"{Dockerfile.Name}:{Dockerfile.Tag}",
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
            }
        };
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

        return new V1Service
        {
            ApiVersion = "v1",
            Kind = "Service",
            Metadata = new V1ObjectMeta
            {
                Name = $"{ResourceName}-service",
                Labels = Defaults.Labels(SolutionName, ResourceName)
            },
            Spec = new V1ServiceSpec
            {
                Selector = Defaults.Labels(SolutionName, ResourceName),
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

public enum AspireResourceType
{
    Unknown,
    Project,
    Container,
    Parameter,
    Value
}
