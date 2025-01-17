using a2k.Cli.Helpers;
using a2k.Cli.Services;
using a2k.Shared.Models;
using System.CommandLine;

namespace a2k.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        CommandLineHelpers.Greet();

        var rootCommand = CommandLineHelpers.WireUp<string, string>(RunDeploymentAsync);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeploymentAsync(string appHost, string @namespace)
    {
        var aspireSolution = new AspireSolution(appHost, @namespace);
        aspireSolution.CreateManifestIfNotExists();
        await aspireSolution.ReadManifest();

        try
        {
            Console.WriteLine("[INFO] Logging in to Docker...");
            ShellService.RunCommand("login", "docker");

            //Console.WriteLine("[INFO] Deploying resources to Kubernetes...");
            //var k8sService = new KubernetesService();
            //await k8sService.DeployManifestAsync(manifest, @namespace, solutionName, resourceToImageMap);

            //Console.WriteLine("[INFO] Deployment completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
    }
}