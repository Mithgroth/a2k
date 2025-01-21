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

        var rootCommand = Helpers.WireUp<string, string, string, bool>(RunDeployment);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeployment(string appHostPath, string name, string env, bool useVersioning = false)
    {
        var solution = new Solution(appHostPath, name, env, useVersioning);
        await solution.ReadManifest();

        DockerLogin();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .Start("[bold blue]Deploying resources to Kubernetes...[/]",
                async ctx =>
                {
                    var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());
                    await solution.Deploy(k8s);
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