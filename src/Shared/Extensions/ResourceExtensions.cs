using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Autorest;
using Spectre.Console;
using System.Net;

namespace a2k.Shared.Extensions;

public static class ResourceExtensions
{
    public static async Task Deploy(this List<Resource> resources, Kubernetes k8s, TreeNode node, LiveDisplayContext ctx)
    {
        var tasks = resources
                    .Where(r => r.ResourceType is AspireResourceTypes.Project or AspireResourceTypes.Container)
                    .Select(x => new { Resource = x, Task = x.DeployResource(k8s) })
                    .ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks.Select(t => t.Task));
            var result = await completedTask;

            result.WriteToConsole(node, ctx);
            tasks.RemoveAll(t => t.Task == completedTask);
        }
    }

    public static async Task DeployServices(this List<Resource> resources, Kubernetes k8s, TreeNode node, LiveDisplayContext ctx)
    {
        var tasks = resources
                    .Where(x => x.ResourceType is AspireResourceTypes.Project or AspireResourceTypes.Container)
                    .Select(x => new { Resource = x, Task = x.DeployService(k8s) })
                    .ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks.Select(t => t.Task));
            var result = await completedTask;

            result.WriteToConsole(node, ctx);
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
            await new Traefik(k8s).Deploy();
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