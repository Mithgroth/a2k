using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Net;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a static value resource in .NET Aspire environment
/// </summary>
public record Value(Solution Solution,
                    string ResourceName,
                    string StaticValue,
                    Dictionary<string, ResourceInput> Inputs)
    : Resource(Solution, ResourceName, null, null, null, AspireResourceTypes.Value)
{
    public override async Task<Result> DeployResource(k8s.Kubernetes k8s)
    {
        bool IsSecret() => Inputs != null && Inputs.TryGetValue("value", out var valueInput) && valueInput.Secret;
        if (IsSecret())
        {
            var secret = new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta
                {
                    Name = ResourceName,
                    NamespaceProperty = Solution.Name,
                    Labels = Defaults.Labels(Solution.Name, ResourceName)
                },
                StringData = new Dictionary<string, string>
                {
                    { "value", StaticValue ?? "" }
                }
            };

            try
            {
                await k8s.ReadNamespacedSecretAsync(secret.Metadata.Name, Solution.Name);

                await k8s.DeleteNamespacedSecretAsync(secret.Metadata.Name, Solution.Name);
                await k8s.CreateNamespacedSecretAsync(secret, Solution.Name);

                return new(Outcome.Replaced, ResourceName);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedSecretAsync(secret, Solution.Name);
                return new(Outcome.Created, ResourceName);
            }
            catch (Exception ex)
            {
                return new(Outcome.Failed, ResourceName, ex);
            }
        }
        else
        {
            var configMap = new V1ConfigMap
            {
                ApiVersion = "v1",
                Kind = "ConfigMap",
                Metadata = new V1ObjectMeta
                {
                    Name = ResourceName,
                    NamespaceProperty = Solution.Name,
                    Labels = Defaults.Labels(Solution.Name, ResourceName)
                },
                Data = new Dictionary<string, string>
                {
                    { "value", StaticValue ?? "" }
                }
            };

            try
            {
                await k8s.ReadNamespacedConfigMapAsync(configMap.Metadata.Name, Solution.Name);

                await k8s.DeleteNamespacedConfigMapAsync(configMap.Metadata.Name, Solution.Name);
                await k8s.CreateNamespacedConfigMapAsync(configMap, Solution.Name);

                return new(Outcome.Replaced, ResourceName);
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedConfigMapAsync(configMap, Solution.Name);
                return new(Outcome.Created, ResourceName);
            }
            catch (Exception ex)
            {
                return new(Outcome.Failed, ResourceName, ex);
            }
        }
    }
}