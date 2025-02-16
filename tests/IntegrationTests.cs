using a2k;
using a2k.Models;
using k8s.Models;

namespace IntegrationTests;

public class DeploymentTests
{
    [Test]
    public async Task DeployWebAppWithDatabase()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true
        });

        var webApp = builder.AddDeployment("webapp", deployment =>
            {
                deployment.Spec.Template.Spec.Containers.Add(new V1Container
                {
                    Name = "webapp",
                    Image = "myregistry/webapp:latest"
                });
            })
            .WithKubernetesMetadata(metadata =>
            {
                metadata.Labels.Add("environment", "test");
            });

        using var app = builder.Build();
        await app.StartAsync();

        try
        {
            await webApp.Resource.ProvisioningTaskCompletionSource!.Task;
            
            await Assert.That(webApp.Resource.Metadata.Labels["environment"]).IsEqualTo("test");
            await Assert.That(webApp.Resource).IsTypeOf<Deployment>();
        }
        finally
        {
            await app.StopAsync();
        }
    }
} 