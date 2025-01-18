using a2k.Shared.Models.Aspire;
using k8s;
using k8s.Models;
using Spectre.Console;

namespace a2k.Shared.Models.Kubernetes;

public class Deployment(Solution AspireSolution)
{
    private readonly k8s.Kubernetes k8s = new(KubernetesClientConfiguration.BuildConfigFromConfigFile());
    private Dictionary<string, string> CommonLabels { get; set; } = new Dictionary<string, string>
        {
            { "app.kubernetes.io/name", AspireSolution.Name },
            { "app.kubernetes.io/managed-by", "a2k" }
        };

    public async Task<ResourceOperationResult> CheckNamespace(bool shouldCreateIfNotExists = true)
    {
        try
        {
            await k8s.ReadNamespaceAsync(AspireSolution.Namespace);
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
                    Name = AspireSolution.Namespace,
                    Labels = CommonLabels,
                }
            };

            await k8s.CreateNamespaceAsync(namespaceObj);
            return ResourceOperationResult.Created;
        }
    }

    /// <summary>
    /// Deploy the resources found in the Aspire manifest. 
    /// In a real scenario, you’d create or update Deployments, Services, Secrets, etc.
    /// </summary>
    public async Task Deploy()
    {
        foreach (var resource in AspireSolution.Resources)
        {
            await resource.Deploy(k8s);
        }
    }

    private async Task HandleParameterResource(string name, ManifestResource resource, string k8sNamespace)
    {
        AnsiConsole.MarkupLine($"[bold gray]Handling parameter resource: {name}[/]");

        // If the parameter is secret (e.g., password), create a Secret
        if (resource.Inputs != null && resource.Inputs.TryGetValue("value", out var paramInput) && paramInput.Secret)
        {
            // The actual password might be in resource.Value or resource.Value might come from the user.
            // If it doesn’t exist yet, we might need to generate. For now, assume it’s already populated.
            var secretValue = resource.Value;
            var secretName = name.Replace("-password", "-secret"); // example naming logic

            var secret = new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta
                {
                    Name = secretName,
                    NamespaceProperty = k8sNamespace
                },
                Data = new Dictionary<string, byte[]>
                {
                    // Convert secretValue to Base64 or keep it as an ASCII
                    { "password", System.Text.Encoding.UTF8.GetBytes(secretValue ?? "") }
                }
            };

            try
            {
                await k8s.CreateNamespacedSecretAsync(secret, k8sNamespace);
            }
            catch
            {
                AnsiConsole.MarkupLine($"[bold yellow]Secret {secretName} already exists or creation failed.[/]");
            }
        }
        else
        {
            // Maybe we store it in a ConfigMap if not secret
            AnsiConsole.MarkupLine($"[bold yellow]Parameter {name} is not marked secret, ignoring or store in ConfigMap if desired.[/]");
        }
    }
}
