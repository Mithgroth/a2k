using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Spectre.Console;
using System.Net;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// 
/// </summary>
/// <param name="Namespace"></param>
/// <param name="SolutionName"></param>
/// <param name="ResourceName"></param>
/// <param name="Bindings"></param>
/// <param name="Env"></param>
public record Parameter(Solution Solution,
                        string ResourceName,
                        string Value,
                        Dictionary<string, ResourceInput> Inputs)
    : Resource(Solution, ResourceName, null, null, null, AspireResourceType.Parameter)
{
    public override async Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s)
    {
        if (IsSecret())
        {
            await DeploySecret();
        }
        else
        {
            await DeployConfigMap();
        }

        return ResourceOperationResult.Created;

        bool IsSecret() => 
            Inputs != null && 
            Inputs.TryGetValue("value", out var paramInput) && 
            paramInput.Secret;

        async Task DeploySecret()
        {
            AnsiConsole.MarkupLine($"[bold gray]Deploying secret parameter: {ResourceName}[/]");

            var secret = new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta
                {
                    Name = ResourceName,
                    NamespaceProperty = Solution.Namespace,
                    Labels = Defaults.Labels(Solution.Name, ResourceName, Solution.Tag)
                },
                Data = new Dictionary<string, byte[]>
                {
                    { "value", System.Text.Encoding.UTF8.GetBytes(Value ?? "") }
                }
            };

            try
            {
                await k8s.ReadNamespacedSecretAsync(secret.Metadata.Name, Solution.Namespace);

                // Always replace secrets to ensure latest value
                await k8s.DeleteNamespacedSecretAsync(secret.Metadata.Name, Solution.Namespace);
                await k8s.CreateNamespacedSecretAsync(secret, Solution.Namespace);

                AnsiConsole.MarkupLine($"[bold blue]Updated secret for {ResourceName}[/]");
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedSecretAsync(secret, Solution.Namespace);
                AnsiConsole.MarkupLine($"[bold green]Created new secret for {ResourceName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error deploying secret {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }

        async Task DeployConfigMap()
        {
            AnsiConsole.MarkupLine($"[bold gray]Deploying config parameter: {ResourceName}[/]");

            var configMap = new V1ConfigMap
            {
                ApiVersion = "v1",
                Kind = "ConfigMap",
                Metadata = new V1ObjectMeta
                {
                    Name = ResourceName,
                    NamespaceProperty = Solution.Namespace,
                    Labels = Defaults.Labels(Solution.Name, ResourceName, Solution.Tag)
                },
                Data = new Dictionary<string, string>
                {
                    { "value", Value ?? "" }
                }
            };

            try
            {
                await k8s.ReadNamespacedConfigMapAsync(configMap.Metadata.Name, Solution.Namespace);

                // Always replace config to ensure latest value
                await k8s.DeleteNamespacedConfigMapAsync(configMap.Metadata.Name, Solution.Namespace);
                await k8s.CreateNamespacedConfigMapAsync(configMap, Solution.Namespace);

                AnsiConsole.MarkupLine($"[bold blue]Updated config for {ResourceName}[/]");
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedConfigMapAsync(configMap, Solution.Namespace);
                AnsiConsole.MarkupLine($"[bold green]Created new config for {ResourceName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error deploying config {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }
}