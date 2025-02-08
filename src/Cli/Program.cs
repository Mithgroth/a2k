using a2k.Cli.CommandLine;
using a2k.Shared;
using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using k8s;
using k8s.Models;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace a2k.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        var rootCommand = new RootCommand
        {
            Description = "Deploy .NET Aspire applications to Kubernetes"
        };

        // Add subcommands
        var deployCommand = new Command("deploy", "Deploy application to Kubernetes")
        {
            Helpers.GetAppHostPathOption(),
            Helpers.GetNameOption(),
            Helpers.GetEnvOption(),
            Helpers.GetVersioningOption(),
            Helpers.GetContextOption(),
            Helpers.GetRegistryUrlOption(),
            Helpers.GetRegistryUserOption(),
            Helpers.GetRegistryPasswordOption()
        };
        deployCommand.Handler = CommandHandler.Create<
            string, string, string, bool, string?, 
            string?, string?, string?>(
                RunDeployment
            );
        
        var statusCommand = new Command("status", "Show deployment status")
        {
            Helpers.GetNameOption(),
            Helpers.GetEnvOption(),
            Helpers.GetContextOption()
        };
        statusCommand.Handler = CommandHandler.Create<string, string, string?>(CheckStatus);
        
        var planCommand = new Command("plan", "Show execution plan without deploying")
        {
            Helpers.GetAppHostPathOption(),
            Helpers.GetNameOption(),
            Helpers.GetEnvOption(),
            Helpers.GetVersioningOption(),
            Helpers.GetContextOption(),
            Helpers.GetRegistryUrlOption(),
            Helpers.GetRegistryUserOption(),
            Helpers.GetRegistryPasswordOption()
        };
        planCommand.Handler = CommandHandler.Create<
            string, string, string, bool, string?,
            string?, string?, string?>(
                ShowPlan
            );

        rootCommand.AddCommand(deployCommand);
        rootCommand.AddCommand(statusCommand);
        rootCommand.AddCommand(planCommand);

        // Add default handler for root command
        rootCommand.SetHandler(() => 
        {
            Helpers.Greet();
            AnsiConsole.Write(new Rule("[green]Usage[/]"));
            
            var table = new Table()
                .Border(TableBorder.None)
                .AddColumn("[yellow]Command[/]")
                .AddColumn("[yellow]Description[/]")
                .AddRow("[green]deploy[/]", "Deploy your application to Kubernetes")
                .AddRow("[green]status[/]", "Check current deployment status")
                .AddRow("[green]plan[/]", "Preview deployment changes")
                .AddRow("[blue]--help[/]", "Show detailed help");
                
            AnsiConsole.Write(table);
            AnsiConsole.Write(new Rule());
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeployment(
        string appHostPath, 
        string name, 
        string env, 
        bool useVersioning,
        string? context = null,
        string? registryUrl = null,
        string? registryUser = null,
        string? registryPassword = null)
    {
        var config = string.IsNullOrEmpty(context)
            ? KubernetesClientConfiguration.BuildConfigFromConfigFile()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: context);
        
        var k8s = new Kubernetes(config);
        var solution = new Solution(appHostPath, name, env, useVersioning, context, registryUrl, registryUser, registryPassword);

        var root = new Tree(Defaults.ROOT);
        await AnsiConsole.Live(root)
            .StartAsync(async ctx =>
            {
                root.AddNode($"[bold]Cluster Context:[/] {config.CurrentContext}");
                root.AddNode($"[bold]Target Cluster:[/] {solution.Context}");
                var phase1 = root.AddNode(Defaults.PHASE_I);
                phase1.AddNode($"[dim]Checking .NET Aspire manifest.json file[/]");
                ctx.Refresh();

                solution.CreateManifestIfNotExists().WriteToConsole(ctx, phase1);

                var result = await solution.ReadManifest();
                result.WriteToConsole(ctx, phase1);

                Shell.DockerLogin();

                result = await solution.CheckNamespace(k8s);
                result.WriteToConsole(ctx, phase1);

                var phase2 = root.AddNode(Defaults.PHASE_II);
                ctx.Refresh();

                result = await solution.DeployConfigurations(k8s);
                result.WriteToConsole(ctx, phase2);

                await solution.Resources.Deploy(k8s, ctx, phase2);

                var phase3 = root.AddNode(Defaults.PHASE_III);
                ctx.Refresh();

                await solution.Resources.DeployServices(k8s, ctx, phase3);

                var phase4 = root.AddNode(Defaults.PHASE_IV);
                ctx.Refresh();

                result = await solution.DeployIngressController(k8s);
                result.WriteToConsole(ctx, phase4);

                result = await solution.DeployIngress(k8s);
                result.WriteToConsole(ctx, phase4);

                var phase5 = root.AddNode(Defaults.PHASE_V);
                ctx.Refresh();

                if (!useVersioning)
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
                    await Helpers.CreateImagePullSecret(k8s, solution);
                }

                root.AddNode($"[bold green]{Emoji.Known.CheckMark} Deployment completed![/]");
            });
    }

    private static async Task CheckStatus(string name, string env, string? context = null)
    {
        var config = string.IsNullOrEmpty(context)
            ? KubernetesClientConfiguration.BuildConfigFromConfigFile()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: context);
        
        var k8s = new Kubernetes(config);
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Context")
            .AddColumn("Namespace")
            .AddColumn("Environment")
            .AddColumn("Pods")
            .AddColumn("Services");

        var namespaces = string.IsNullOrEmpty(name) 
            ? await k8s.ListNamespaceAsync() 
            : new V1NamespaceList(items: [await k8s.ReadNamespaceAsync(name)]);

        foreach (var ns in namespaces.Items)
        {
            var nsName = ns.Metadata.Name;
            
            // Filter by env label if specified
            if (!string.IsNullOrEmpty(env) && 
                (ns.Metadata.Labels?.TryGetValue("app.kubernetes.io/environment", out var envLabel) ?? false) &&
                envLabel != env)
            {
                continue;
            }

            var pods = await k8s.ListNamespacedPodAsync(nsName);
            var services = await k8s.ListNamespacedServiceAsync(nsName);
            var ingresses = await k8s.ListNamespacedIngressAsync(nsName);

            table.AddRow(
                config.CurrentContext,
                nsName,
                (ns.Metadata.Labels?.TryGetValue("app.kubernetes.io/environment", out envLabel) ?? false) ? envLabel : "default",
                $"[green]{pods.Items.Count} running[/]",
                $"{services.Items.Count} active"
            );
        }

        AnsiConsole.Write(table);
    }

    private static async Task ShowPlan(
        string appHostPath,
        string name,
        string env,
        bool useVersioning,
        string? context = null,
        string? registryUrl = null,
        string? registryUser = null,
        string? registryPassword = null)
    {
        var solution = new Solution(
            appHostPath, 
            name, 
            env, 
            useVersioning, 
            context,
            registryUrl,
            registryUser,
            registryPassword
        );
        
        var panel = new Panel(new Rows(
            new Markup($"[bold]Application:[/] {solution.Name}"),
            new Markup($"[bold]Environment:[/] {solution.Env}"),
            new Markup($"[bold]Cluster:[/] {solution.Context}"),
            new Markup($"[bold]Registry:[/] {(solution.IsLocal ? "local" : solution.RegistryUrl)}"),
            new Markup($"[bold]Versioning:[/] {(solution.UseVersioning ? "Enabled" : "Disabled")}")
        ));
        
        var deploymentPlan = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Action")
            .AddColumn("Resource")
            .AddColumn("Type")
            .AddColumn("Details");

        // Docker images section
        var dockerResources = solution.Resources.Where(r => r.Dockerfile != null).ToList();
        if (dockerResources.Any())
        {
            deploymentPlan.AddRow("[yellow]CREATE[/]", "Docker Images", "", "");
            foreach (var resource in dockerResources)
            {
                var imageName = solution.IsLocal 
                    ? resource.Dockerfile.Name 
                    : $"{solution.RegistryUrl}/{resource.Dockerfile.Name}";
                
                deploymentPlan.AddRow(
                    "  [green]+[/]", 
                    resource.ResourceName, 
                    "Image",
                    $"{imageName}:{resource.Dockerfile.Tag} ({(solution.IsLocal ? "local build" : "remote registry")})"
                );
            }
        }

        // Kubernetes resources section
        var k8sResources = solution.Resources.ToList();
        if (k8sResources.Any())
        {
            deploymentPlan.AddRow("[yellow]CREATE[/]", "Kubernetes Resources", "", "");
            foreach (var resource in k8sResources)
            {
                deploymentPlan.AddRow(
                    "  [green]+[/]", 
                    resource.ResourceName, 
                    resource.ResourceType.ToString(),
                    resource.Dockerfile?.FullImageName ?? "external service"
                );
            }
        }

        // Configuration section
        deploymentPlan.AddRow("[yellow]UPDATE[/]", "Cluster Configuration", "", "");
        deploymentPlan.AddRow("  [blue]~[/]", "Namespace", "Kubernetes", solution.Name);
        deploymentPlan.AddRow("  [blue]~[/]", "Ingress", "Traefik", $"{solution.Name}-ingress");

        AnsiConsole.Write(panel);
        AnsiConsole.Write(deploymentPlan);
        
        AnsiConsole.MarkupLine("\n[dim]Plan:[/]");
        AnsiConsole.MarkupLine($"[green]{dockerResources.Count} Docker images[/] to build");
        AnsiConsole.MarkupLine($"[green]{k8sResources.Count} Kubernetes resources[/] to deploy");
        AnsiConsole.MarkupLine($"[blue]1 namespace[/] to create/update");
        AnsiConsole.MarkupLine($"[yellow]1 ingress controller[/] to configure\n");
    }
}