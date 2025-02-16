using k8s;
using k8s.Models;

public class HelmChart : Resource, IKubernetesResource
{
    public string ChartName { get; }
    public string Version { get; }
    public Dictionary<string, object> Values { get; } = new();
    public KubernetesClientConfiguration? KubernetesConfig { get; set; }
    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
    public override V1ObjectMeta Metadata => new() { Name = Name };

    public HelmChart(string name, string chartName, string version) 
        : base(name)
    {
        ChartName = chartName;
        Version = version;
    }
    
    public override async Task ApplyAsync(k8s.Kubernetes client, CancellationToken cancellationToken = default)
    {
        // Implementation will be handled in HelmProvisioner
    }
} 