using a2k.Cli.Models;
using a2k.Cli.Services;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;

namespace a2k.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new FigletText("a2k").Color(Color.HotPink));
        AnsiConsole.MarkupLine("[bold lime]Deploy .NET Aspire to Kubernetes![/]");
        AnsiConsole.WriteLine();

        // Define options
        var appHostOption = new Option<string>(
            "--appHost",
            description: "The path to the AppHost project folder",
            getDefaultValue: Directory.GetCurrentDirectory);

        var namespaceOption = new Option<string>(
            "--namespace",
            description: "The Kubernetes namespace to deploy to",
            getDefaultValue: () => "default");

        // Define the root command
        var rootCommand = new RootCommand
        {
            appHostOption,
            namespaceOption
        };

        rootCommand.Description = "a2k CLI: Deploy Aspire projects to Kubernetes";

        // Handle execution
        rootCommand.Handler = CommandHandler.Create<string, string>(RunDeploymentAsync);

        // Invoke command
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeploymentAsync(string appHost, string @namespace)
    {
        var manifestPath = Path.Combine(appHost, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"manifest.json file not found at {manifestPath}, creating...");
            ShellService.RunCommand("run --publisher manifest --output-path manifest.json", "dotnet");
            Console.WriteLine($"manifest.json file created!");
        }

        try
        {
            Console.WriteLine($"[INFO] Loading manifest from: {manifestPath}");
            var json = await File.ReadAllTextAsync(manifestPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var manifest = JsonSerializer.Deserialize<AspireManifest>(json, options);
            if (manifest == null)
            {
                Console.WriteLine($"Error: Failed to parse manifest at {manifestPath}");
                return;
            }

            Console.WriteLine("[INFO] Logging in to Docker...");
            ShellService.RunCommand("login", "docker");

            var imagesToBuild = DockerService.FindImagesToBuild(manifest, appHost);
            foreach (var (name, context, dockerfile, isProject) in imagesToBuild)
            {
                var imageName = $"{name}:latest";

                if (isProject)
                {
                    Console.WriteLine($"[INFO] Building .NET project {name}...");
                    var publishCommand = $"publish {context} -c Release --verbosity quiet --os linux /t:PublishContainer";
                    ShellService.RunCommand(publishCommand, "dotnet");
                    Console.WriteLine($"[INFO] Published Docker image for {name}...");
                }
                else
                {
                    Console.WriteLine($"[INFO] Building image for {name} with Dockerfile...");
                    var buildCommand = $"build -t {imageName} -f {dockerfile} {context}";
                    ShellService.RunCommand(buildCommand, "docker");
                }
            }

            Console.WriteLine("[INFO] Deploying resources to Kubernetes...");
            var k8sService = new KubernetesService();
            await k8sService.DeployManifestAsync(manifest, @namespace);

            Console.WriteLine("[INFO] Deployment completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
    }
}