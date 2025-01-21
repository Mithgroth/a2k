using Aspire.Hosting;
using Spectre.Console;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace a2k.Shared.Models.Aspire;

/// <summary>
/// Represents a .NET project in Aspire environment
/// </summary>
public record Project(Solution Solution,
                      string ResourceName,
                      string CsProjPath,
                      bool UseVersioning,
                      Dockerfile Dockerfile,
                      Dictionary<string, ResourceBinding> Bindings,
                      Dictionary<string, string> Env)
    : Resource(Solution, ResourceName, Dockerfile, Bindings, Env, AspireResourceTypes.Project)
{
    public override async Task<Result> DeployResource(k8s.Kubernetes k8s)
    {
        // Build .NET Project
        var publishCommand = $"dotnet publish {Directory.GetParent(CsProjPath)} -c Release --verbosity quiet --os linux /t:PublishContainer /p:ContainerRepository={Dockerfile.Name}";
        if (Solution.UseVersioning)
        {
            publishCommand += $" /p:ContainerImageTags={Dockerfile.Tag}";
        }

        Shell.Run(publishCommand);

        var sha256 = Shell.Run($"docker inspect --format={{{{.Id}}}} {Dockerfile.FullImageName}", writeToOutput: false).Replace("sha256:", "").Trim();
        Dockerfile = Dockerfile.UpdateSHA256(sha256);

        var baseResult = await base.DeployResource(k8s);
        baseResult?.Messages.Prepend(new Markup($"[bold green]Published Docker image for {ResourceName} as {Dockerfile.FullImageName}[/]"));

        return baseResult;
    }

    //public override V1Service ToKubernetesService()
    //{
    //    var resource = Defaults.V1Service(ResourceName, Solution.Env, Solution.Tag);
        
    //    // Check if we have any external bindings
    //    var hasExternalBindings = Bindings?.Values.Any(b => b.External ?? false) ?? false;
        
    //    // Try to get port from launchSettings.json if we have bindings
    //    var port = GetProjectPort() ?? 80;

    //    resource.Spec = new V1ServiceSpec
    //    {
    //        Type = "ClusterIP",
    //        Selector = Defaults.SelectorLabels(Solution.Env),
    //        Ports = [new() { Port = port, TargetPort = 80 }] // targetPort is always 80 (container port)
    //    };

    //    return resource;
    //}

    private int? GetProjectPort()
    {
        // Only look for ports if we have bindings marked as external
        if (Bindings == null || !Bindings.Values.Any(b => b.External ?? false))
        {
            return null;
        }

        // First check if any external binding has a port specified
        var bindingWithPort = Bindings.Values.FirstOrDefault(b => b.External ?? false && b.Port.HasValue);
        if (bindingWithPort?.Port != null)
        {
            return bindingWithPort.Port;
        }

        // If no port in manifest, check launchSettings.json
        var projectDir = Path.GetDirectoryName(CsProjPath);
        var launchSettingsPath = Path.Combine(projectDir!, "Properties", "launchSettings.json");

        if (File.Exists(launchSettingsPath))
        {
            try
            {
                var launchSettings = JsonSerializer.Deserialize<LaunchSettings>(
                    File.ReadAllText(launchSettingsPath), 
                    Defaults.JsonSerializerOptions);

                foreach (var profile in launchSettings?.Profiles?.Values ?? Enumerable.Empty<LaunchProfile>())
                {
                    if (string.IsNullOrEmpty(profile.ApplicationUrl))
                    {
                        continue;
                    }

                    var urls = profile.ApplicationUrl.Split(';');

                    // Try to find an HTTP URL first if we have an HTTP binding
                    if (Bindings.Values.Any(b => (b.External ?? false) && b.Scheme == "http"))
                    {
                        var httpUrl = urls.FirstOrDefault(u => u.StartsWith("http://"));
                        if (httpUrl != null)
                        {
                            return ExtractPort(httpUrl);
                        }
                    }

                    // If no HTTP URL found or we only have HTTPS binding, use the first HTTPS URL
                    if (Bindings.Values.Any(b => (b.External ?? false) && b.Scheme == "https"))
                    {
                        var httpsUrl = urls.FirstOrDefault(u => u.StartsWith("https://"));
                        if (httpsUrl != null)
                        {
                            return ExtractPort(httpsUrl);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not read launch settings for {ResourceName}: {ex.Message}[/]");
            }
        }

        // If we have external bindings but no port specified anywhere, generate a random port
        return Utility.GenerateRandomPort();

        static int ExtractPort(string url)
        {
            var match = Regex.Match(url, @":(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
            {
                return port;
            }

            throw new InvalidOperationException("Invalid port format in launch settings");
        }
    }
}