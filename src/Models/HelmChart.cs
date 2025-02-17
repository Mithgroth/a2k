using Aspire.Hosting.ApplicationModel;
using k8s;

namespace a2k.Models;

public class HelmChart : IResource
{
    public string ChartName { get; }
    public string Version { get; }
    public Dictionary<string, object> Values { get; } = [];
    public KubernetesClientConfiguration? KubernetesConfig { get; set; }
    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }


    public ResourceAnnotationCollection Annotations => Annotations;

    public string Name => ChartName;

    public HelmChart(string name, string chartName, string version)
    {
        ChartName = chartName;
        Version = version;
    }

    public async Task ApplyAsync(Kubernetes client, CancellationToken cancellationToken = default)
    {
        // Implementation will be handled in HelmProvisioner
    }
}