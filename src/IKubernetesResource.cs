using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.Models;

namespace a2k;

public interface IKubernetesResource : IResourceWithEnvironment
{
    TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
    KubernetesClientConfiguration? KubernetesConfig { get; set; }
    V1ObjectMeta Metadata { get; }
    Task ApplyAsync(Kubernetes client, CancellationToken cancellationToken = default);
}