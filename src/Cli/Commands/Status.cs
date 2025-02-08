using k8s;
using k8s.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace a2k.Cli.Commands;

public sealed class Status : AsyncCommand<Status.StatusSettings>
{
    public sealed class StatusSettings : CommandSettings
    {
        [CommandOption("-n|--name <NAME>")]
        [Description("Application/Namespace name")]
        public string? Name { get; init; }

        [CommandOption("-e|--env <ENV>")]
        [Description("Deployment environment filter")]
        public string? Env { get; init; }

        [CommandOption("-c|--context <CONTEXT>")]
        [Description("Kubernetes context name")]
        public string? Context { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Context")
            .AddColumn("Namespace")
            .AddColumn("Environment")
            .AddColumn("Pods")
            .AddColumn("Services");

        var config = string.IsNullOrEmpty(settings.Context)
            ? KubernetesClientConfiguration.BuildConfigFromConfigFile()
            : KubernetesClientConfiguration.BuildConfigFromConfigFile(currentContext: settings.Context);

        var k8s = new Kubernetes(config);
        var namespaces = string.IsNullOrEmpty(settings.Name)
            ? await k8s.ListNamespaceAsync()
            : new V1NamespaceList(items: [await k8s.ReadNamespaceAsync(settings.Name)]);

        foreach (var ns in namespaces.Items)
        {
            var nsName = ns.Metadata.Name;

            // Filter by env label if specified
            if (!string.IsNullOrEmpty(settings.Env) &&
                (ns.Metadata.Labels?.TryGetValue("app.kubernetes.io/environment", out var envLabel) ?? false) &&
                envLabel != settings.Env)
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

        return 0;
    }
} 