using k8s;
using k8s.Models;

namespace a2k.Models;

public class Deployment : Resource, IKubernetesResource
{
    private readonly V1Deployment _deployment;
    private Action<V1Deployment>? _configureDeployment;
    public KubernetesClientConfiguration? KubernetesConfig { get; set; }
    public TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
    public override V1ObjectMeta Metadata => _deployment.Metadata;

    public Deployment(string name) : base(name)
    {
        _deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = name,
                Labels = new Dictionary<string, string>()
            {
                { "app", name},
                { "app.kubernetes.io/managed-by", "a2k"}
            }
            },
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
            }
        };
    }

    public void Configure(Action<V1Deployment> configure)
    {
        _configureDeployment = configure;
    }

    public override async Task ApplyAsync(Kubernetes client, CancellationToken cancellationToken = default)
    {
        _configureDeployment?.Invoke(_deployment);

        try
        {
            await client.AppsV1.CreateNamespacedDeploymentAsync(
                _deployment,
                "default",
                cancellationToken: cancellationToken);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            await client.AppsV1.ReplaceNamespacedDeploymentAsync(
                _deployment,
                _deployment.Metadata.Name,
                "default",
                cancellationToken: cancellationToken);
        }
    }
}