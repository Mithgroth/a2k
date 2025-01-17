using k8s;
using k8s.Models;

namespace a2k.Shared.Models;

public class KubernetesDeployment
{
    private readonly Kubernetes k8s = new(KubernetesClientConfiguration.BuildConfigFromConfigFile());

    public string Namespace { get; set; }
    public Dictionary<string, string> CommonLabels { get; set; }

    public KubernetesDeployment(AspireSolution aspireSolution)
    {
        Namespace = aspireSolution.Namespace;
        CommonLabels = new Dictionary<string, string>
        {
            { "app.kubernetes.io/name", aspireSolution.Name },
            { "app.kubernetes.io/managed-by", "a2k" }
        };
    }

    public async Task<ResourceOperationResult> CheckNamespace(bool shouldCreateIfNotExists = true)
    {
        try
        {
            await k8s.ReadNamespaceAsync(Namespace);
            return ResourceOperationResult.Exists;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            if (!shouldCreateIfNotExists)
            {
                return ResourceOperationResult.Missing;
            }

            var namespaceObj = new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = Namespace,
                    Labels = CommonLabels,
                }
            };

            await k8s.CoreV1.CreateNamespaceAsync(namespaceObj);
            return ResourceOperationResult.Created;
        }
    }
}
