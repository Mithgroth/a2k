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
            Helpers.GetVersioningOption()
        };
        deployCommand.Handler = CommandHandler.Create<string, string, string, bool>(RunDeployment);
        
        var statusCommand = new Command("status", "Show deployment status")
        {
            new Option<string>("--name", "Namespace to check"),
            new Option<string>("--env", "Environment filter"),
        };
        statusCommand.Handler = CommandHandler.Create<string, string>(CheckStatus);
        
        var planCommand = new Command("plan", "Show execution plan without deploying")
        {
            Helpers.GetAppHostPathOption(),
            Helpers.GetNameOption(),
            Helpers.GetEnvOption(),
            Helpers.GetVersioningOption()
        };
        planCommand.Handler = CommandHandler.Create<string, string, string, bool>(ShowPlan);

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
        bool useVersioning = false)
    {
        var solution = new Solution(appHostPath, name, env, useVersioning);
        var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());

        var root = new Tree(Defaults.ROOT);
        await AnsiConsole.Live(root)
            .StartAsync(async ctx =>
            {
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

                root.AddNode($"[bold green]{Emoji.Known.CheckMark} Deployment completed![/]");
            });
    }

    private static async Task CheckStatus(string name, string env)
    {
        var k8s = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile());
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Namespace")
            .AddColumn("Environment")
            .AddColumn("Pods")
            .AddColumn("Services")
            .AddColumn("Ingress");

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
                nsName,
                (ns.Metadata.Labels?.TryGetValue("app.kubernetes.io/environment", out envLabel) ?? false) ? envLabel : "default",
                $"[green]{pods.Items.Count} running[/]",
                $"{services.Items.Count} active",
                ingresses.Items.Count > 0 ? Emoji.Known.CheckMark : Emoji.Known.CrossMark
            );
        }

        AnsiConsole.Write(table);
    }

    private static async Task ShowPlan(
        string appHostPath, 
        string name, 
        string env, 
        bool useVersioning = false)
    {
        // Initialize solution with manifest parsing
        var solution = new Solution(appHostPath, name, env, useVersioning);
        
        // Add this line to load resources from manifest
        await solution.ReadManifest();

        var panel = new Panel(new Rows(
            new Markup($"[bold]Application:[/] {solution.Name}"),
            new Markup($"[bold]Environment:[/] {solution.Env}"),
            new Markup($"[bold]Versioning:[/] {(solution.UseVersioning ? "Enabled" : "Disabled")}"),
            new Markup($"[bold]Tag:[/] {solution.Tag}")
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
                deploymentPlan.AddRow(
                    "  [green]+[/]", 
                    resource.ResourceName, 
                    "Image",
                    $"{resource.Dockerfile.FullImageName} (new build)"
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