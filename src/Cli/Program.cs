using a2k.Cli.CommandLine;
using a2k.Cli.ManifestParsing;
using a2k.Shared;
using a2k.Shared.Models.Aspire;
using a2k.Shared.Models.Kubernetes;
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
        var solution = new Solution(appHost, @namespace);
        await solution.ReadManifest();

        try
        {
            AnsiConsole.MarkupLine("[blue]Logging in to [bold]Docker...[/][/]");
            Shell.Run("docker login");

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .Start("[bold blue]Deploying resources to Kubernetes...[/]", async ctx => {
                    var deployment = new Deployment(solution);

                    var namespaceResult = await deployment.CheckNamespace();
                    AnsiConsole.MarkupLine($"[bold {Helpers.PickColourForResult(namespaceResult)}]Checking namespace: {namespaceResult}[/]");

                    await deployment.Deploy();
                });

            AnsiConsole.MarkupLine("[bold green]:thumbs_up: Deployment completed![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]ERROR: {ex.Message}[/]");
        }
    }
}