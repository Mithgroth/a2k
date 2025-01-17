namespace a2k.Shared.Models;

/// <summary>
/// Represents a container resource in .NET Aspire environment (redis, postgres, etc)
/// </summary>
public class AspireContainer : AspireResource
{
    public AspireContainer(string name) : base(name)
    {
        Type = AspireResourceType.Container;
    }

    public Dockerfile? Dockerfile { get; set; }

    public Dictionary<string, ResourceBinding> Bindings { get; set; }
    public Dictionary<string, string> Env { get; set; }
}