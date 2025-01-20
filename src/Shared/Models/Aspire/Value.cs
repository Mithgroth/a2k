using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Spectre.Console;
using System.Net;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a static value resource in .NET Aspire environment
/// </summary>
public record Value(Solution Solution,
                    string ResourceName,
                    string StaticValue,
                    Dictionary<string, ResourceInput> Inputs)
    : Resource(Solution, ResourceName, null, null, null, AspireResourceType.Value)
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
            Inputs.TryGetValue("value", out var valueInput) && 
            valueInput.Secret;

        async Task DeploySecret()
        {
            AnsiConsole.MarkupLine($"[bold gray]Deploying secret value: {ResourceName}[/]");

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
                StringData = new Dictionary<string, string>
                {
                    { "value", StaticValue ?? "" }
                }
            };

            try
            {
                await k8s.ReadNamespacedSecretAsync(secret.Metadata.Name, Solution.Namespace);

                // Always replace secrets to ensure latest value
                await k8s.DeleteNamespacedSecretAsync(secret.Metadata.Name, Solution.Namespace);
                await k8s.CreateNamespacedSecretAsync(secret, Solution.Namespace);

                AnsiConsole.MarkupLine($"[bold blue]Updated secret value for {ResourceName}[/]");
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedSecretAsync(secret, Solution.Namespace);
                AnsiConsole.MarkupLine($"[bold green]Created new secret value for {ResourceName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error deploying secret value {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }

        async Task DeployConfigMap()
        {
            AnsiConsole.MarkupLine($"[bold gray]Deploying config value: {ResourceName}[/]");

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
                    { "value", StaticValue ?? "" }
                }
            };

            try
            {
                await k8s.ReadNamespacedConfigMapAsync(configMap.Metadata.Name, Solution.Namespace);

                // Always replace config to ensure latest value
                await k8s.DeleteNamespacedConfigMapAsync(configMap.Metadata.Name, Solution.Namespace);
                await k8s.CreateNamespacedConfigMapAsync(configMap, Solution.Namespace);

                AnsiConsole.MarkupLine($"[bold blue]Updated config value for {ResourceName}[/]");
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                await k8s.CreateNamespacedConfigMapAsync(configMap, Solution.Namespace);
                AnsiConsole.MarkupLine($"[bold green]Created new config value for {ResourceName}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Error deploying config value {Markup.Escape(ResourceName)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }
} 