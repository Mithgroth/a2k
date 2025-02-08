using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using a2k.Shared.Models.Kubernetes;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Net;
using System.Text;

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
        return
        [
            new Option<string>(
                ["--appHostPath", "-p"],
                description: "Path to Aspire AppHost project",
                getDefaultValue: Directory.GetCurrentDirectory),
            
            new Option<string>(
                ["--name", "-n"],
                description: "Application/Namespace name"),
            
            new Option<string>(
                ["--env", "-e"],
                description: "Deployment environment (dev, stg, prod)",
                getDefaultValue: () => "default"),
            
            new Option<bool>(
                ["--useVersioning", "-v"],
                description: "Enable Kubernetes revision tracking"),
            
            new Option<string>(
                ["--context", "-c"],
                description: "Kubernetes context name (default: current context)"),
            
            new Option<string>(
                ["--registry-url"],
                description: "Docker registry URL (e.g. docker.io, ghcr.io)"),
            
            new Option<string>(
                ["--registry-user"],
                description: "Docker registry username"),
            
            new Option<string>(
                ["--registry-password"],
                description: "Docker registry password")
        ];
    }

    internal static RootCommand WireUp<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> handler)
    {
        var options = ConfigureOptions();
        
        var rootCommand = new RootCommand
        {
            options[0], // appHostPath
            options[1], // name
            options[2], // env
            options[3], // useVersioning
            options[4], // context
            options[5], // registry-url
            options[6], // registry-user
            options[7], // registry-password
        };

        rootCommand.Description = "a2k CLI: Deploy Aspire projects to Kubernetes";
        rootCommand.Handler = CommandHandler.Create(handler);

        return rootCommand;
    }

    public static Option<string> GetAppHostPathOption() 
        => new(["--appHostPath", "-a"],
               description: "Path to Aspire AppHost project",
               getDefaultValue: Directory.GetCurrentDirectory);

    public static Option<string> GetNameOption() => new(["--name", "-n"], "Application/Namespace name");

    public static Option<string> GetEnvOption() 
        => new(["--env", "-e"],
               description: "Deployment environment (dev, stg, prod)",
               getDefaultValue: () => "default");

    public static Option<bool> GetVersioningOption() => new(["--useVersioning", "-v"], "Enable Kubernetes revision tracking");

    public static Option<string> GetContextOption() 
        => new(["--context", "-c"],
            description: "Kubernetes context name (default: current context)")
        {
            IsRequired = false
        };

    public static Option<string> GetRegistryUrlOption() 
        => new(["--registry-url"], "Docker registry URL (e.g. docker.io, ghcr.io)");

    public static Option<string> GetRegistryUserOption() 
        => new(["--registry-user"], "Docker registry username");

    public static Option<string> GetRegistryPasswordOption() 
        => new(["--registry-password"], "Docker registry password");

    public static async Task<Result> CreateImagePullSecret(Kubernetes k8s, Solution solution)
    {
        var secret = new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = "a2k-registry-creds",
                NamespaceProperty = solution.Name
            },
            Type = "kubernetes.io/dockerconfigjson",
            Data = new Dictionary<string, byte[]>
            {
                [".dockerconfigjson"] = Encoding.UTF8.GetBytes($$"""
                    {
                        "auths": {
                            "{{solution.RegistryUrl}}": {
                                "username": "{{solution.RegistryUser}}",
                                "password": "{{solution.RegistryPassword}}",
                                "auth": "{{Convert.ToBase64String(Encoding.UTF8.GetBytes($"{solution.RegistryUser}:{solution.RegistryPassword}"))}}"
                            }
                        }
                    }
                    """)
            }
        };

        try
        {
            await k8s.CreateNamespacedSecretAsync(secret, solution.Name);
            return new(Outcome.Created, "Secret/a2k-registry-creds");
        }
        catch
        {
            return new(Outcome.Exists, "Secret/a2k-registry-creds");
        }
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