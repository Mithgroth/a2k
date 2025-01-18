using a2k.Cli.CommandLine;
using a2k.Cli.ManifestParsing;
using a2k.Shared.Models;
using Spectre.Console;
using System.CommandLine;

namespace a2k.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Helpers.Greet();

        var rootCommand = Helpers.WireUp<string, string>(RunDeployment);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeployment(string appHost, string @namespace)
    {
        var aspireSolution = new AspireSolution(appHost, @namespace);
        await aspireSolution.ReadManifest();

        try
        {
            AnsiConsole.MarkupLine("[bold blue]Logging in to Docker...[/]");
            Shell.Run("docker login");

            AnsiConsole.MarkupLine("[bold blue]Deploying resources to Kubernetes...[/]");

            var k8sDeployment = new KubernetesDeployment(aspireSolution);
            
            var namespaceResult = await k8sDeployment.CheckNamespace();
            AnsiConsole.MarkupLine($"[bold {Helpers.PickColourForResult(namespaceResult)}]Checking namespace: {namespaceResult}[/]");

            await k8sDeployment.Deploy();
            AnsiConsole.MarkupLine("[bold green]Deployment completed![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]ERROR: {ex.Message}[/]");
        }
    }
}