namespace a2k.Shared.Models;

public sealed class AspireSolution
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

    public ICollection<AspireResource> Resources { get; set; } = [];

    public AspireSolution(string appHost, string? @namespace)
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
}
