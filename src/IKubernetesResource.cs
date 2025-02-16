using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.Models;

public interface IKubernetesResource : IResource
{
    TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
    KubernetesClientConfiguration? KubernetesConfig { get; set; }
    V1ObjectMeta Metadata { get; }
}