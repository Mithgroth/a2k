using Spectre.Console;

namespace a2k.Shared.Models;

/// <summary>
/// Represents a .NET project in Aspire environment
/// </summary>
public record AspireProject(string SolutionName,
                            string ResourceName,
                            string CsProjPath,
                            Dockerfile? Dockerfile,
                            Dictionary<string, ResourceBinding> Bindings,
                            Dictionary<string, string> Env) 
    : AspireResource(SolutionName, ResourceName, Dockerfile, Bindings, Env, AspireResourceType.Project)
{
    public void PublishContainer()
    {
        AnsiConsole.MarkupLine($"[bold gray]Building .NET project {ResourceName}...[/]");
        Shell.Run($"dotnet publish {Directory.GetParent(CsProjPath)} -c Release --verbosity quiet --os linux /t:PublishContainer /p:ContainerRepository={Dockerfile.Name.Replace(":latest", "")}");
        AnsiConsole.MarkupLine($"[bold green]Published Docker image for {ResourceName} as {Dockerfile.Name.Replace(":latest", "")}[/]");
    }
}