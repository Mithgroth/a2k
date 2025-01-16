using k8s;
using System.Diagnostics;

namespace E2E;

public class k8sDeployment
{
    private readonly string TestNamespace = "e2e-tests";
    private readonly string AspireProjectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "AspireApp1", "AspireApp1.AppHost");
    private readonly string CliProjectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "bin", "a2k.Cli", "debug", "a2k.Cli.exe");
    private readonly Kubernetes _kubernetes;

    public k8sDeployment()
    {
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        _kubernetes = new Kubernetes(config);
    }

    [Fact]
    public async Task A2KCli_ShouldDeployAspireApp1Successfully()
    {
        // Arrange
        var cliExecutablePath = Path.GetFullPath(CliProjectPath);
        var appHostPath = Path.GetFullPath(AspireProjectPath);

        if (!File.Exists(cliExecutablePath))
        {
            throw new Exception($"CLI executable not found: {cliExecutablePath}");
        }

        if (!Directory.Exists(appHostPath))
        {
            throw new Exception($"AppHost path not found: {appHostPath}");
        }

        Console.WriteLine("[INFO] Running a2k CLI to deploy AspireApp1...");
        RunShellCommand(cliExecutablePath, $"--appHost {appHostPath} --namespace {TestNamespace}", workingDirectory: appHostPath);

        Console.WriteLine("[INFO] Verifying deployment...");
        await WaitForPodsReady(TestNamespace);

        // Assert
        var pods = await _kubernetes.ListNamespacedPodAsync(TestNamespace);
        Assert.NotEmpty(pods.Items);
        Assert.All(pods.Items, pod => Assert.Equal("Running", pod.Status.Phase));
    }

    private void RunShellCommand(string command, string arguments, string? workingDirectory = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
            }
        };

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Console.WriteLine(stdout);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Command '{command} {arguments}' failed: {stderr}");
        }
    }


    private async Task WaitForPodsReady(string ns)
    {
        const int maxRetries = 10;
        var retries = 0;

        while (retries < maxRetries)
        {
            var pods = await _kubernetes.ListNamespacedPodAsync(ns);
            if (pods.Items.All(pod => pod.Status.Phase == "Running"))
            {
                return;
            }

            retries++;
            await Task.Delay(5000);
        }

        throw new Exception($"Pods in namespace {ns} did not become ready in time.");
    }

    [Fact]
    public async Task CleanupTestNamespace()
    {
        Console.WriteLine($"[INFO] Cleaning up namespace: {TestNamespace}");
        await _kubernetes.DeleteNamespaceAsync(TestNamespace);
    }
}
