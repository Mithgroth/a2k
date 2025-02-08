using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace a2k.Shared.Settings;

public sealed class DeploySettings : CommandSettings
{
    [CommandOption("-a|--appHostPath <PATH>")]
    [Description("Path to Aspire AppHost project")]
    [DefaultValue(".")]
    public string AppHostPath { get; init; } = ".";

    [CommandOption("-n|--name <NAME>")]
    [Description("Application/Namespace name")]
    public string? Name { get; init; }

    [CommandOption("-e|--env <ENV>")]
    [Description("Deployment environment (dev, stg, prod)")]
    [DefaultValue("default")]
    public string Env { get; init; } = "default";

    [CommandOption("-v|--useVersioning")]
    [Description("Enable Kubernetes revision tracking")]
    public bool UseVersioning { get; init; }

    [CommandOption("-c|--context <CONTEXT>")]
    [Description("Kubernetes context name (default: current context)")]
    public string? Context { get; init; }

    [CommandOption("--registry-url <URL>")]
    [Description("Docker registry URL")]
    public string? RegistryUrl { get; init; }

    [CommandOption("--registry-user <USER>")]
    [Description("Docker registry username")]
    public string? RegistryUser { get; init; }

    [CommandOption("--registry-password <PASSWORD>")]
    [Description("Docker registry password")]
    public string? RegistryPassword { get; init; }

    public override ValidationResult Validate()
    {
        if (!string.IsNullOrEmpty(RegistryUrl) &&
            (string.IsNullOrEmpty(RegistryUser) || string.IsNullOrEmpty(RegistryPassword)))
        {
            return ValidationResult.Error("Registry credentials required for remote deployments");
        }

        return ValidationResult.Success();
    }
}
