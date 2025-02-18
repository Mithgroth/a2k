using a2k;
using a2k.Deployment;
using a2k.Models;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using System.Net;

namespace UnitTests;

public class KubernetesDeployerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly Mock<Kubernetes> _k8sClientMock = new();
    private readonly KubernetesDeployer _deployer;

    public KubernetesDeployerTests()
    {
        var context = new KubernetesContext(new KubernetesOptions { Namespace = "test" });
        _mocker.Use(context);
        _deployer = _mocker.CreateInstance<KubernetesDeployer>();
    }

    [Test]
    public async Task DeployAsync_CreatesNewDeployment_WhenNotExists()
    {
        // Arrange
        var deployment = new Deployment("test-deployment", "test");
        
        _k8sClientMock.Setup(k => k.ReadNamespacedDeploymentAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpOperationException("Not found") 
            { 
                Response = new HttpResponseMessageWrapper(
                    new HttpResponseMessage(HttpStatusCode.NotFound), 
                    "") 
            });

        _mocker.Use<IKubernetes>(_k8sClientMock.Object);

        // Act
        await _deployer.DeployAsync(deployment, CancellationToken.None);

        // Assert
        _k8sClientMock.Verify(k => k.CreateNamespacedDeploymentAsync(
            deployment, 
            "test",
            null,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Test]
    public async Task DeployAsync_UpdatesExistingDeployment_WhenExists()
    {
        // Arrange
        var deployment = new Deployment("test-deployment", "test");

        _k8sClientMock.Setup(k => k.ReadNamespacedDeploymentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new V1Deployment());

        _mocker.Use(_k8sClientMock.Object);

        // Act
        await _deployer.DeployAsync(deployment, CancellationToken.None);

        // Assert
        _k8sClientMock.Verify(k => k.ReplaceNamespacedDeploymentAsync(
            deployment, 
            "test-deployment", 
            "test",
            null,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()
            ), Times.Once);
    }
}

public class FakeLoggerFactory : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider) { }
    public ILogger CreateLogger(string categoryName) => new FakeLogger();
    public void Dispose() { }
}

public class FakeLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
} 