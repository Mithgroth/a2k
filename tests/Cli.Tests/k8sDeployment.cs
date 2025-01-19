using a2k.Shared;
using k8s;

namespace k8sDeployment;

public class Test : IDisposable
{
    private readonly string TestNamespace = "e2e-tests";
    private readonly string CliProjectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "bin", "a2k.Cli", "debug", "a2k.Cli.exe");
    private readonly Kubernetes _kubernetes;

    public Test()
    {
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        _kubernetes = new Kubernetes(config);
    }

    [Theory]
    [InlineData("AspireApp1")]
    public async Task DeployAspireApps(string appName)
    {
        // Arrange
        var aspireProjectPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", appName, $"{appName}.AppHost");
        var cliExecutablePath = Path.GetFullPath(CliProjectPath);
        var appHostPath = Path.GetFullPath(aspireProjectPath);

        if (!File.Exists(cliExecutablePath))
        {
            throw new Exception($"CLI executable not found: {cliExecutablePath}");
        }

        if (!Directory.Exists(appHostPath))
        {
            throw new Exception($"AppHost path not found: {appHostPath}");
        }

        // Act
        Shell.Run($"{cliExecutablePath} --namespace {TestNamespace}", workingDirectory: appHostPath);
        await WaitForPodsReady();

        // Assert
        var pods = await _kubernetes.ListNamespacedPodAsync(TestNamespace);
        Assert.NotEmpty(pods.Items);
        Assert.All(pods.Items, pod => Assert.Equal("Running", pod.Status.Phase));
    }

    private async Task WaitForPodsReady()
    {
        const int maxRetries = 10;
        var retries = 0;

        while (retries < maxRetries)
        {
            var pods = await _kubernetes.ListNamespacedPodAsync(TestNamespace);
            if (pods.Items.All(pod => pod.Status.Phase == "Running"))
            {
                return;
            }

            retries++;
            await Task.Delay(5000);
        }

        throw new Exception($"Pods in namespace {TestNamespace} did not become ready in time.");
    }

    public void Dispose()
    {
        _kubernetes.DeleteNamespaceAsync(TestNamespace).Wait();
    }
}
