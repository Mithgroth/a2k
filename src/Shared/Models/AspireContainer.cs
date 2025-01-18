namespace a2k.Shared.Models;

/// <summary>
/// Represents a container resource in .NET Aspire environment (redis, postgres, etc)
/// </summary>
public record AspireContainer(string SolutionName,
                              string ResourceName,
                              Dockerfile? Dockerfile,
                              Dictionary<string, ResourceBinding> Bindings,
                              Dictionary<string, string> Env)
    : AspireResource(SolutionName, ResourceName, Dockerfile, Bindings, Env, AspireResourceType.Container)
{

}