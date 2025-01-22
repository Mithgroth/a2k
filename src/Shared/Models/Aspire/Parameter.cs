using k8s;
using k8s.Autorest;
using k8s.Models;
using System.Net;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// 
/// </summary>
/// <param name="Solution"></param>
/// <param name="ResourceName"></param>
/// <param name="Value"></param>
/// <param name="Inputs"></param>
public record Parameter(Solution Solution,
                        string ResourceName,
                        string Value,
                        Dictionary<string, ResourceInput> Inputs)
    : Resource(Solution, ResourceName, null, null, null, AspireResourceTypes.Parameter)
{
    public override async Task<Result> DeployResource(k8s.Kubernetes k8s)
    {
        bool IsSecret() => Inputs != null && Inputs.TryGetValue("value", out var paramInput) && paramInput.Secret;
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
                    Labels = Defaults.Labels(Solution.Name, Solution.Tag)
                },
                Data = new Dictionary<string, byte[]>
                {
                    { "value", System.Text.Encoding.UTF8.GetBytes(Value ?? "") }
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
                    Labels = Defaults.Labels(Solution.Env, Solution.Tag)
                },
                Data = new Dictionary<string, string>
                {
                    { "value", Value ?? "" }
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