using a2k.Shared.Models.Kubernetes;
using k8s;

namespace a2k.Shared.Models.Aspire;

public sealed record Solution
{
    /// <summary>
    /// Represents .sln file name in a .NET Aspire solution, used as Application name in Kubernetes
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string Namespace { get; set; } = "default";

    /// <summary>
    /// AppHost folder path of the .NET Aspire solution
    /// </summary>
    public string AppHostPath { get; set; } = string.Empty;

    /// <summary>
    /// manifest.json path of the AppHost project
    /// </summary>
    public string ManifestPath { get; set; } = string.Empty;
    public Manifest? Manifest { get; set; }

    public ICollection<Resource> Resources { get; set; } = [];

    public Solution(string appHost, string? @namespace)
    {
        // TODO: Ensure it is a .NET Aspire solution

        AppHostPath = appHost ?? throw new ArgumentNullException(nameof(appHost));
        ManifestPath = Path.Combine(appHost, "manifest.json");
        Name = Path.GetFileName(Directory.GetParent(appHost)?.FullName ?? "aspire-app").Replace(".sln", string.Empty);

        if (!string.IsNullOrEmpty(@namespace))
        {
            Namespace = @namespace;
        }
    }

    public async Task<ResourceOperationResult> CheckNamespace(k8s.Kubernetes k8s, bool shouldCreateIfNotExists = true)
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

            await k8s.CreateNamespaceAsync(Defaults.V1Namespace(Namespace, Name));
            return ResourceOperationResult.Created;
        }
    }
}
