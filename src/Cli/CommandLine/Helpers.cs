using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Autorest;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Net;

namespace a2k.Cli.CommandLine;

internal static class Helpers
{
    internal static void Greet()
    {
        AnsiConsole.Write(new FigletText("a2k").Color(Color.MediumPurple1).Centered());
        AnsiConsole.Write(new Markup("[slowblink plum4]Deploy .NET Aspire to Kubernetes![/]").Centered());
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule());
        AnsiConsole.WriteLine();
    }

    private static IList<Option> ConfigureOptions()
    {
        var appHostOption = new Option<string>(
            "--appHostPath",
            description: "The path to the AppHost project folder",
            getDefaultValue: Directory.GetCurrentDirectory);

        var nameOption = new Option<string>(
            "--name",
            description: "Name of your application, deployed to Kubernetes as Namespace. Defaults to your Aspire project's sln file name.",
            getDefaultValue: () => string.Empty);

        var envOption = new Option<string>(
            "--env",
            description: "Environment you are deploying as (dev, stg, prod), deployed to Kubernetes as Application. Defaults to your \"default\" if not specified.",
            getDefaultValue: () => string.Empty);

        var versioningOption = new Option<bool>(
            "--useVersioning",
            description: "Use versioning while deploying to Kubernetes and Docker, if this is false a2k only uses latest tag",
            getDefaultValue: () => false);

        return [appHostOption, nameOption, envOption, versioningOption];
    }

    internal static RootCommand WireUp<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> handler)
    {
        var options = ConfigureOptions();

        // Define the root command
        var rootCommand = new RootCommand
        {
            // TODO: Find a better way to wire this up
            options[0],
            options[1],
            options[2],
            options[3],
        };

        rootCommand.Description = "a2k CLI: Deploy Aspire projects to Kubernetes";
        rootCommand.Handler = CommandHandler.Create(handler);

        return rootCommand;
    }
}

public static class ResultExtensions
{
    public static void WriteToConsole(this Result result, LiveDisplayContext ctx, TreeNode node)
    {
        node.AddNodes(result.Messages);
        ctx.Refresh();
    }
}

public static class ResourceExtensions
{
    public static async Task Deploy(this List<Resource> resources, Kubernetes k8s, LiveDisplayContext ctx, TreeNode node)
    {
        var tasks = resources
                    .Where(r => r.ResourceType is AspireResourceTypes.Project or AspireResourceTypes.Container)
                    .Select(x => new { Resource = x, Task = x.DeployResource(k8s) })
                    .ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks.Select(t => t.Task));
            var result = await completedTask;

            result.WriteToConsole(ctx, node);
            tasks.RemoveAll(t => t.Task == completedTask);
        }
    }

    public static async Task DeployServices(this List<Resource> resources, Kubernetes k8s, LiveDisplayContext ctx, TreeNode node)
    {
        var tasks = resources
                    .Where(x => x.ResourceType is AspireResourceTypes.Project or AspireResourceTypes.Container)
                    .Select(x => new { Resource = x, Task = x.DeployService(k8s) })
                    .ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks.Select(t => t.Task));
            var result = await completedTask;

            result.WriteToConsole(ctx, node);
            tasks.RemoveAll(t => t.Task == completedTask);
        }
    }

    public static async Task<Result> DeployIngressController(this Solution solution, Kubernetes k8s)
    {
        var externalBindings = solution.GetExternalBindings();
        if (!externalBindings.Any())
        {
            return new(Outcome.Skipped, [new Markup("[dim]No external bindings found, skipping ingress setup[/]")]);
        }

        // Deploy Traefik if not exists
        try
        {
            await k8s.ReadNamespacedDeploymentAsync("traefik-deployment", "kube-system");
            return new(Outcome.Exists, [new Markup("[blue]Traefik is already installed[/]")]);
        }
        catch (HttpOperationException)
        {
            await Traefik.Deploy(k8s);
            return new(Outcome.Created, [new Markup("[green]Traefik installed successfully[/]")]);
        }
    }

    public static async Task<Result> DeployIngress(this Solution solution, Kubernetes k8s)
    {
        var externalBindings = solution.GetExternalBindings();
        if (!externalBindings.Any())
        {
            return new(Outcome.Skipped, [new Markup("[dim]No external bindings found, skipping ingress setup[/]")]);
        }

        // Create single ingress for all services
        var ingress = Ingress.Create(solution.Name, externalBindings);
        try
        {
            await k8s.ReadNamespacedIngressAsync(ingress.Metadata.Name, solution.Name);

            await k8s.DeleteNamespacedIngressAsync(ingress.Metadata.Name, solution.Name);
            await k8s.CreateNamespacedIngressAsync(ingress, solution.Name);

            return new(Outcome.Replaced, ingress.Metadata.Name);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            await k8s.CreateNamespacedIngressAsync(ingress, solution.Name);
            return new(Outcome.Created, ingress.Metadata.Name);
        }
        catch (Exception ex)
        {
            return new(Outcome.Failed, ingress.Metadata.Name, ex);
        }
    }
}