using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using a2k.Shared.Settings;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace a2k.Cli.Commands;

public sealed class Plan : AsyncCommand<DeploySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeploySettings settings)
    {
        var solution = new Solution(settings);
        solution.CreateManifestIfNotExists();
        var manifestResult = await solution.ReadManifest();
        
        if (manifestResult.Outcome != Outcome.Succeeded)
        {
            AnsiConsole.MarkupLine($"[red]Error reading manifest: {manifestResult.Messages.FirstOrDefault()}[/]");
            return 1;
        }

        // Main layout grid
        var grid = new Grid()
            .AddColumns(3)
            .AddRow(
                new Panel("[bold]Aspire Components[/]").BorderColor(Color.Green),
                new Text(""),
                new Panel("[bold]Kubernetes Resources[/]").BorderColor(Color.Blue)
            );

        foreach (var resource in solution.Resources)
        {
            (var left, IRenderable right) = resource switch
            {
                Project p => (
                    new Markup($"[green]▷ {p.ResourceName}[/]\n[dim]ASP.NET Project[/]"),
                    new Rows(
                        new Markup($"📦 [blue]Deployment/Pod → {p.ResourceName}[/]"),
                        new Markup($"🔗 [yellow]Service → {p.ResourceName}-service[/]"),
                        new Markup($"[grey]●[/] ConfigMap → {p.ResourceName}-config")
                    )
                ),
                Container c => (
                    new Markup($"[blue]▷ {c.ResourceName}[/]\n[dim]Container Image[/]"),
                    new Rows(
                        new Markup($"📦 [blue]Deployment/Pod → {c.ResourceName}[/]"),
                        new Markup($"🔗 [yellow]Service → {c.ResourceName}-service[/]")
                    )
                ),
                _ => (
                    new Markup(""),
                    new Rows()
                )
            };

            grid.AddRow(
                new Panel(left).BorderColor(Color.Grey),
                new Markup("[bold]→[/]"),
                new Panel(right).BorderColor(Color.Grey)
            );
        }

        // Add cluster-wide resources
        grid.AddRow(
            new Panel(new Markup("[grey]Network Gateway[/]")).BorderColor(Color.Grey),
            new Markup("[bold]→[/]"),
            new Panel(new Rows(
                new Markup($"🌐 [purple]Ingress: {solution.Name}-ingress[/]"),
                new Markup("[dim]Routes external traffic to services[/]"),
                new Markup(GetExternalUrls(solution))
            )).BorderColor(Color.Grey)
        );

        grid.AddRow(
            new Panel(new Markup("[grey]Cluster Traffic[/]")).BorderColor(Color.Grey),
            new Markup("[bold]→[/]"),
            new Panel(new Rows(
                new Markup($"🛡 [red]Ingress Controller: traefik[/]"),
                new Markup("[dim]Manages external access (auto-installed)[/]"),
                new Markup("Namespace: kube-system")
            )).BorderColor(Color.Grey)
        );

        // Header
        AnsiConsole.Write(new Panel(grid)
            .Header($"[bold]   Deployment Plan: {solution.Name} → {solution.Context}   [/]")
            .BorderColor(Color.White));

        // Legend
        AnsiConsole.Write(new Panel(new Rows(
            new Markup("[grey]●[/] [blue]Deployment/Pod[/] = Application instance"),
            new Markup("[grey]●[/] [yellow]Service[/] = Network endpoint"),
            new Markup("[grey]●[/] [purple]Ingress[/] = Public access point (URLs shown below)"),
            new Markup("[grey]●[/] [red]Controller[/] = Cluster infrastructure")
        )).BorderColor(Color.Grey));

        return 0;
    }

    private static string GetIngressUrl(Solution solution)
    {
        var externalBindings = solution.GetExternalBindings();
        if (!externalBindings.Any()) return "[yellow]No external endpoints[/]";
        
        return string.Join("\n", externalBindings.Select(b => 
            $"[link]http://{b.Resource.ResourceName}.{solution.Name}.local:{b.Port}[/]"));
    }

    private static string GetExternalUrls(Solution solution)
    {
        var urls = solution.GetExternalBindings()
            .Select(b => $"[link]http://{b.Resource.ResourceName}.{solution.Name}.local:{b.Port}[/]");
        
        return urls.Any() 
            ? $"🔗 [bold]External URLs:[/]\n{string.Join("\n", urls)}" 
            : "[yellow]No external endpoints configured[/]";
    }
} 