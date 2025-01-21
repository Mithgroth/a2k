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
        var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());

        var root = new Tree("[bold lightseagreen]Deploying resources to Kubernetes[/]");
        await AnsiConsole.Live(root)
            .StartAsync(async ctx =>
            {
                var phase1 = root.AddNode($"[bold]Phase 1 - Preparing[/]");
                phase1.AddNode($"[bold gray]Checking .NET Aspire manifest.json file[/]");
                ctx.Refresh();

                solution.CreateManifestIfNotExists().WriteToConsole(ctx, phase1);

                var result = await solution.ReadManifest();
                result.WriteToConsole(ctx, phase1);

                Shell.DockerLogin();

                result = await solution.CheckNamespace(k8s);
                result.WriteToConsole(ctx, phase1);

                var phase2 = root.AddNode($"[bold]Phase 2 - Deploying Resources[/]");
                // TODO: Fix the bug "[bold lightseagreen]Deploying resources to Kubernetes[/]" being doubled somehow
                await solution.Resources.Deploy(k8s, ctx, phase2);

                var phase3 = root.AddNode($"[bold]Phase 3 - Deploying Services for Resources[/]");
                await solution.Resources.DeployServices(k8s, ctx, phase3);

                var phase4 = root.AddNode($"[bold]Phase 4 - Configuring Ingress Bindings[/]");
                // WIP:
                // CHECK IF WE HAVE ANY EXTERNAL BINDINGS
                // --> TURN THEM INTO INGRESS RULES
                // --> INSTALL TRAEFIK
                // --> DEPLOY INGRESS RULES

                var phase5 = root.AddNode($"[bold]Phase 5 - Testing Node Status[/]");

                //AnsiConsole.MarkupLine("[bold green]Deployment completed![/]");
            });
    }
}