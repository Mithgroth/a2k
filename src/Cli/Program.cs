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

        var rootCommand = Helpers.WireUp<string, string>(RunDeploymentAsync);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeploymentAsync(string appHost, string @namespace)
    {
        var aspireSolution = new AspireSolution(appHost, @namespace);
        aspireSolution.CreateManifestIfNotExists();
        await aspireSolution.ReadManifest();

        try
        {
            AnsiConsole.MarkupLine("[blue]Logging in to Docker...[/]");
            Shell.Run("docker login");

            //Console.WriteLine("[INFO] Deploying resources to Kubernetes...");
            //var k8sService = new KubernetesService();
            //await k8sService.DeployManifestAsync(manifest, @namespace, solutionName, resourceToImageMap);

            //Console.WriteLine("[INFO] Deployment completed!");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red][ERROR] {ex.Message}[/]");
        }
    }
}