namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a container resource in .NET Aspire environment (redis, postgres, etc)
/// </summary>
public record Container(Solution Solution,
                        string ResourceName,
                        bool UseVersioning,
                        Dockerfile? Dockerfile,
                        Dictionary<string, ResourceBinding> Bindings,
                        Dictionary<string, string> Env)
    : Resource(Solution, ResourceName, Dockerfile, Bindings, Env, AspireResourceTypes.Container)
{
    public override async Task<Result> DeployResource(k8s.Kubernetes k8s)
    {
        if (Dockerfile?.ShouldBuildWithDocker == true)
        {
            var buildCommand = $"docker build -t {Dockerfile.FullImageName} --label a2k.project={Solution.Name}";
            if (!string.IsNullOrEmpty(Dockerfile?.Path))
            {
                buildCommand += $" -f {Dockerfile.Path}";
            }

            if (!string.IsNullOrEmpty(Dockerfile?.Context))
            {
                buildCommand += $" {Dockerfile.Context}";
            }

            Shell.Run(buildCommand, writeToOutput: false);

            var sha256 = Shell.Run($"docker inspect --format={{{{.Id}}}} {Dockerfile.Name}", writeToOutput: false).Replace("sha256:", "").Trim();
            Dockerfile = Dockerfile.UpdateSHA256(sha256);
        }

        return await base.DeployResource(k8s);
    }
}