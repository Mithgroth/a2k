using a2k.Shared.Models.Kubernetes;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace a2k.Cli.CommandLine;

internal static class Helpers
{
    internal static void Greet()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new FigletText("a2k").Color(Color.HotPink));
        AnsiConsole.MarkupLine("[bold lime]Deploy .NET Aspire to Kubernetes![/]");
        AnsiConsole.WriteLine();
    }

    private static IList<Option> ConfigureOptions()
    {
        var appHostOption = new Option<string>(
            "--appHost",
            description: "The path to the AppHost project folder",
            getDefaultValue: Directory.GetCurrentDirectory);

        var namespaceOption = new Option<string>(
            "--namespace",
            description: "The Kubernetes namespace to deploy to",
            getDefaultValue: () => string.Empty);

        return [appHostOption, namespaceOption];
    }

    internal static RootCommand WireUp<T1, T2>(Func<T1, T2, Task> handler)
    {
        var options = ConfigureOptions();

        // Define the root command
        var rootCommand = new RootCommand
        {
            // TODO: Find a better way to wire this up
            options[0],
            options[1]
        };

        rootCommand.Description = "a2k CLI: Deploy Aspire projects to Kubernetes";
        rootCommand.Handler = CommandHandler.Create(handler);

        return rootCommand;
    }

    internal static string PickColourForResult(ResourceOperationResult result) => result switch
    {
        ResourceOperationResult.Created => "green",
        ResourceOperationResult.Exists => "white",
        ResourceOperationResult.Replaced => "yellow",
        ResourceOperationResult.Deleted => "red",
        ResourceOperationResult.Missing => "gray",
        ResourceOperationResult.Failed => "red",
        _ => "white"
    };
}
