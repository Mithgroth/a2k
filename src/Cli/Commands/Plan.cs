using a2k.Shared.Models.Aspire;
using a2k.Shared.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace a2k.Cli.Commands;

public sealed class Plan : Command<DeploySettings>
{
    public override int Execute(CommandContext context, DeploySettings settings)
    {
        var solution = new Solution(settings);
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
        if (dockerResources.Count != 0)
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

        return 0;
    }
} 