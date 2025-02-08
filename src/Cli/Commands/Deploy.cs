using a2k.Shared;
using a2k.Shared.Extensions;
using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using a2k.Shared.Settings;
using k8s;
using Spectre.Console;
using Spectre.Console.Cli;

namespace a2k.Cli.Commands;

public sealed class Deploy : AsyncCommand<DeploySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeploySettings settings)
    {
        var config = string.IsNullOrEmpty(settings.Context)
            ? KubernetesClientConfiguration.BuildConfigFromConfigFile()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: settings.Context);

        var k8s = new Kubernetes(config);
        var solution = new Solution(settings);

        var root = new Tree(Defaults.ROOT);
        await AnsiConsole.Live(root)
            .StartAsync(async ctx =>
            {
                root.AddNode($"[bold]Cluster Context:[/] {config.CurrentContext}");
                root.AddNode($"[bold]Target Cluster:[/] {solution.Context}");
                var phase1 = root.AddNode(Defaults.PHASE_I);
                phase1.AddNode($"[dim]Checking .NET Aspire manifest.json file[/]");
                ctx.Refresh();

                solution.CreateManifestIfNotExists().WriteToConsole(phase1, ctx);

                var result = await solution.ReadManifest();
                result.WriteToConsole(phase1, ctx);

                Shell.DockerLogin();

                result = await solution.CheckNamespace(k8s);
                result.WriteToConsole(phase1, ctx);

                var phase2 = root.AddNode(Defaults.PHASE_II);
                ctx.Refresh();

                result = await solution.DeployConfigurations(k8s);
                result.WriteToConsole(phase2, ctx);

                await solution.Resources.Deploy(k8s, phase2, ctx);

                var phase3 = root.AddNode(Defaults.PHASE_III);
                ctx.Refresh();

                await solution.Resources.DeployServices(k8s, phase3, ctx);

                var phase4 = root.AddNode(Defaults.PHASE_IV);
                ctx.Refresh();

                result = await solution.DeployIngressController(k8s);
                result.WriteToConsole(phase4, ctx);

                result = await solution.DeployIngress(k8s);
                result.WriteToConsole(phase4, ctx);

                var phase5 = root.AddNode(Defaults.PHASE_V);
                ctx.Refresh();

                if (!settings.UseVersioning)
                {
                    phase5.AddNode("[dim]Cleaning up old Docker images...[/]");
                    ctx.Refresh();

                    // Wait for image building to be completed
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    Dockerfile.CleanupAll(solution.Name);

                    phase5.AddNode($"[bold gray]{Emoji.Known.LitterInBinSign} Final image cleanup completed![/]");
                    ctx.Refresh();
                }

                if (!solution.IsLocal)
                {
                    var secret = Defaults.ImagePullSecret(solution);

                    try
                    {
                        await k8s.CreateNamespacedSecretAsync(secret, solution.Name);
                        //return new(Outcome.Created, "Secret/a2k-registry-creds");
                    }
                    catch
                    {
                        //return new(Outcome.Exists, "Secret/a2k-registry-creds");
                    }
                }

                root.AddNode($"[bold green]{Emoji.Known.CheckMark} Deployment completed![/]");
            });

        return 0;
    }
} 