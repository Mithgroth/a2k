using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.Models;

namespace a2k.Models;

public abstract class Resource(string name) : IKubernetesResource
{
    public string Name { get; } = name;

    public ResourceAnnotationCollection Annotations { get; } = [];

    public KubernetesClientConfiguration? KubernetesConfig { get; set; }

    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public abstract V1ObjectMeta Metadata { get; }

    public abstract Task ApplyAsync(Kubernetes client, CancellationToken cancellationToken = default);
}