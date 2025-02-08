﻿using Aspire.Hosting;
using k8s.Models;
using Spectre.Console;
using System.Text.Json;

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
        var registryHost = Solution.RegistryUrl?
            .Replace("https://", "")
            .Replace("http://", "")
            .TrimEnd('/');

        // Remove solution name prefix from project name
        var projectName = Dockerfile.Name.Replace($"{Solution.Name}-", "");
        
        var imageName = Solution.IsLocal 
            ? projectName 
            : $"{registryHost}/{Solution.RegistryNamespace}/{projectName}";

        // Add validation output
        AnsiConsole.MarkupLine($"[yellow]Building image: {imageName}:{Dockerfile.Tag}[/]");
        AnsiConsole.MarkupLine($"[yellow]Full image: {imageName}:{Dockerfile.Tag}[/]");

        var publishCommand = $"dotnet publish {Directory.GetParent(CsProjPath)} -c Release " +
            $"--os linux /t:PublishContainer " +
            $"/p:ContainerRepository={imageName} " +
            $"/p:ContainerImageTags={Dockerfile.Tag}";

        Shell.Run(publishCommand);

        if (!Solution.IsLocal)
        {
            Shell.Run($"docker login {Solution.RegistryUrl} -u {Solution.RegistryUser} -p {Solution.RegistryPassword}", writeToOutput: true);
            Shell.Run($"docker push {imageName}:{Dockerfile.Tag}", writeToOutput: true);
        }

        var sha256 = Shell.Run($"docker inspect --format={{{{.Id}}}} {Dockerfile.FullImageName}", writeToOutput: false).Replace("sha256:", "").Trim();
        Dockerfile = Dockerfile.UpdateSHA256(sha256);

        // Update deployment to use final image name
        Dockerfile = Dockerfile with { Name = imageName, Tag = Dockerfile.Tag };
        
        var baseResult = await base.DeployResource(k8s);
        baseResult?.Messages.Prepend(new Markup($"[bold green]Published Docker image for {ResourceName} as {Dockerfile.FullImageName}[/]"));

        return baseResult;
    }

    public override V1Service ToKubernetesService()
    {
        var resource = Defaults.V1Service(ResourceName, Solution.Env, Solution.Tag);

        // Get the port from bindings or defaults
        var port = GetProjectPort() ?? throw new InvalidOperationException($"No valid port found for {ResourceName}");

        resource.Spec = new V1ServiceSpec
        {
            Type = "ClusterIP",
            Selector = Defaults.Labels(Solution.Env, Solution.Tag),
            Ports =
            [
                new V1ServicePort
                {
                    Port = port,
                    TargetPort = Bindings?.Values.FirstOrDefault(b => b.TargetPort.HasValue)?.TargetPort ?? 80
                }
            ]
        };

        return resource;
    }

    public int? GetProjectPort()
    {
        if (Bindings == null || !Bindings.Values.Any(b => b.External ?? false))
        {
            return null;
        }

        var bindingWithPort = Bindings.Values.FirstOrDefault(b => b.External ?? false && b.Port.HasValue);
        if (bindingWithPort?.Port != null)
        {
            return bindingWithPort.Port;
        }

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
                    if (!string.IsNullOrEmpty(profile.ApplicationUrl))
                    {
                        var urls = profile.ApplicationUrl.Split(';');
                        var url = urls.FirstOrDefault(u => u.StartsWith("http://") || u.StartsWith("https://"));
                        if (url != null)
                        {
                            return Utility.ExtractPort(url);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not read launch settings for {ResourceName}. Falling back to random port. Details: {ex.Message}[/]");
            }
        }

        return Utility.GenerateAvailablePort();
    }
}