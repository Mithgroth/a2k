using Aspire.Hosting.ApplicationModel;
using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Net;

namespace a2k.Models;

public class Deployment : V1Deployment, IKubernetesResource
{
    private Action<V1Deployment>? _configureDeployment;
    public KubernetesClientConfiguration? KubernetesConfig { get; set; }
    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }

    public string Name => Metadata.Name;

    public ResourceAnnotationCollection Annotations => Annotations;

    public Deployment(string name)
    {
        Metadata = new V1ObjectMeta
        {
            Name = name,
            Labels = new Dictionary<string, string>()
                {
                    { "app", name},
                    { "app.kubernetes.io/managed-by", "a2k"}
                }
        };
        ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
        Kind = KubeKind;
        Spec = new V1DeploymentSpec
        {
            Selector = new V1LabelSelector
            {
                MatchLabels = new Dictionary<string, string>
                {
                    ["app"] = name
                }
            },
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        ["app"] = name
                    }
                },
                Spec = new V1PodSpec()
            }
        };
    }

    public void Configure(Action<V1Deployment> configure)
    {
        _configureDeployment = configure;
    }

    public async Task ApplyAsync(Kubernetes client, CancellationToken cancellationToken = default)
    {
        _configureDeployment?.Invoke(this);

        try
        {
            await client.ReadNamespacedDeploymentAsync(Metadata.Name, Metadata.NamespaceProperty, cancellationToken: cancellationToken);
            await client.ReplaceNamespacedDeploymentAsync(this, Metadata.Name, Metadata.NamespaceProperty, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await client.CreateNamespacedDeploymentAsync(this, Metadata.NamespaceProperty, cancellationToken: cancellationToken);
        }
    }
}