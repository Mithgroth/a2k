using a2k.Shared;
using k8s;

namespace E2E;

public class k8sDeployment : IDisposable
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

        Shell.Run($"{cliExecutablePath} --namespace {TestNamespace}", workingDirectory: appHostPath);

        Console.WriteLine("[INFO] Verifying deployment...");
        await WaitForPodsReady(TestNamespace);

        // Assert
        var pods = await _kubernetes.ListNamespacedPodAsync(TestNamespace);
        Assert.NotEmpty(pods.Items);
        Assert.All(pods.Items, pod => Assert.Equal("Running", pod.Status.Phase));
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

    //[Fact]
    //public async Task CleanupTestNamespace()
    //{
    //    Console.WriteLine($"[INFO] Cleaning up namespace: {TestNamespace}");
    //    await _kubernetes.DeleteNamespaceAsync(TestNamespace);
    //}

    public void Dispose()
    {
        _kubernetes.DeleteNamespaceAsync(TestNamespace).Wait();
    }
}
