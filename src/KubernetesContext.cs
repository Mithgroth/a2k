using k8s;

namespace a2k;

public class KubernetesContext
{
    public KubernetesClientConfiguration Config { get; }
    public Kubernetes Client { get; }
    public string Namespace { get; }
    public Dictionary<string, string> CommonLabels { get; }

    public KubernetesContext(KubernetesOptions options)
    {
        Config = KubernetesClientConfiguration.BuildDefaultConfig();
        Client = new Kubernetes(Config);
        Namespace = options.Namespace ?? "default";
        CommonLabels = options.CommonLabels;
    }
} 