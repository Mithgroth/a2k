using a2k.Cli.CommandLine;
using a2k.Shared;
using a2k.Shared.Models.Aspire;
using k8s;
using Spectre.Console;
using System.CommandLine;

namespace a2k.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Helpers.Greet();

        var rootCommand = Helpers.WireUp<string, string, bool>(RunDeployment);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeployment(string appHost, string @namespace, bool useVersioning = false)
    {
        var solution = new Solution(appHost, @namespace, useVersioning);
        await solution.ReadManifest();

        DockerLogin();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .Start("[bold blue]Deploying resources to Kubernetes...[/]",
                async ctx =>
                {
                    var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());
                    var namespaceResult = await solution.CheckNamespace(k8s);
                    AnsiConsole.MarkupLine($"[bold {Helpers.PickColourForResult(namespaceResult)}]Checking {@namespace} namespace: {namespaceResult}[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine();

                    foreach (var resource in solution.Resources)
                    {
                        await resource.Deploy(k8s);
                    }
                });

        AnsiConsole.MarkupLine("[bold green]:thumbs_up: Deployment completed![/]");

        static void DockerLogin()
        {
            var output = Shell.Run("docker login", writeToOutput: false);
            var panel = new Panel(output)
            {
                Header = new("[bold deepskyblue1]Docker Login[/]")
            }
            .DoubleBorder()
            .BorderColor(Color.DeepSkyBlue1);

            AnsiConsole.Write(panel);
        }
    }
}