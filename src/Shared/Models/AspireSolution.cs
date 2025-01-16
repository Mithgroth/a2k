using a2k.Shared.Models;

namespace Shared.Models;

public class AspireSolution
{
    /// <summary>
    /// Represents .sln file name in a .NET Aspire solution, used as Application name in Kubernetes.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// .NET Projects in the Aspire solution (these will require docker build)
    /// </summary>
    public IList<AspireProject> Projects { get; set; } = [];

    /// <summary>
    /// Any other resources in Aspire solution (databases, cache, etc)
    /// </summary>
    public IList<AspireResource> Resources { get; set; } = [];
}
