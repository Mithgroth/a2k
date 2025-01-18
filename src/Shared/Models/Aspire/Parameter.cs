using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Models;
using Spectre.Console;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// 
/// </summary>
/// <param name="Namespace"></param>
/// <param name="SolutionName"></param>
/// <param name="ResourceName"></param>
/// <param name="Bindings"></param>
/// <param name="Env"></param>
public record Parameter(string Namespace,
                        string SolutionName,
                        string ResourceName,
                        string Value,
                        Dictionary<string, ResourceInput> Inputs)
    : Resource(SolutionName, ResourceName, null, null, null, AspireResourceType.Parameter)
{
    public override async Task<ResourceOperationResult> Deploy(k8s.Kubernetes k8s)
    {
        AnsiConsole.MarkupLine($"[bold gray]Handling parameter resource: {ResourceName}[/]");

        // If the parameter is secret (e.g., password), create a Secret
        if (Inputs != null && Inputs.TryGetValue("value", out var paramInput) && paramInput.Secret)
        {
            // The actual password might be in resource.Value or resource.Value might come from the user.
            // If it doesn’t exist yet, we might need to generate. For now, assume it’s already populated.
            var secretValue = Value;
            var secretName = ResourceName.Replace("-password", "-secret"); // example naming logic

            var secret = new V1Secret
            {
                ApiVersion = "v1",
                Kind = "Secret",
                Metadata = new V1ObjectMeta
                {
                    Name = secretName,
                    NamespaceProperty = Namespace
                },
                Data = new Dictionary<string, byte[]>
                {
                    // Convert secretValue to Base64 or keep it as an ASCII
                    { "password", System.Text.Encoding.UTF8.GetBytes(secretValue ?? "") }
                }
            };

            try
            {
                await k8s.CreateNamespacedSecretAsync(secret, Namespace);
            }
            catch
            {
                AnsiConsole.MarkupLine($"[bold yellow]Secret {secretName} already exists or creation failed.[/]");
            }

            return ResourceOperationResult.Created;
        }
        else
        {
            // Maybe we store it in a ConfigMap if not secret
            AnsiConsole.MarkupLine($"[bold yellow]Parameter {ResourceName} is not marked secret, ignoring or store in ConfigMap if desired.[/]");
            return ResourceOperationResult.Failed;
        }
    }
}