using a2k.Cli.CommandLine;
using a2k.Cli.Deployment;
using a2k.Cli.ManifestParsing;
using a2k.Shared.Models;
using System.CommandLine;

namespace a2k.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Helpers.Greet();

        var rootCommand = Helpers.WireUp<string, string>(RunDeployment);
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunDeployment(string appHost, string @namespace)
    {
        var aspireSolution = new AspireSolution(appHost, @namespace);
        await aspireSolution.ReadManifest();
        await aspireSolution.Deploy();
    }
}