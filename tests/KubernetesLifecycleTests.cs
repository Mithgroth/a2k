using a2k;
using a2k.Models;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

public class KubernetesLifecycleTests
{
    [Test]
    public async Task LifecycleHook_DeploysResources_WhenConfigured()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true
        });

        builder.UseKubernetes(options => options.Namespace = "test");

        var testDeployment = new Deployment("test-deployment", "test");
        await builder.AddResource(testDeployment).ToKubernetes();

        // Act
        using var app = builder.Build();
        var notificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3000));
        
        await app.StartAsync(cts.Token);

        // Assert
        await foreach (var notification in notificationService.WatchAsync(cts.Token))
        {
            if (notification.Resource == testDeployment && notification.Snapshot.State?.Text is "Deployed" or "Failed")
            {
                await Assert.That(notification.Snapshot.State?.Text).IsEqualTo("Deployed");
                return;
            }
        }
        
        Assert.Fail("Did not receive deployment completion notification");
    }

    [Test]
    public async Task ApiService_DeploysToKubernetes()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true
        });

        var apiService = await builder.AddProject<Projects.AspireApp1_ApiService>("apiservice")
            .ToKubernetes();

        builder.UseKubernetes(options => 
        {
            options.Namespace = "integration-test";
        });

        using var app = builder.Build();
        var notificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        await app.StartAsync(cts.Token);

        try
        {
            await foreach (var notification in notificationService.WatchAsync(cts.Token))
            {
                if (notification.Resource == apiService.Resource && 
                    notification.Snapshot.State?.Text is "Deployed" or "Failed")
                {
                    await Assert.That(notification.Snapshot.State?.Text).IsEqualTo("Deployed");
                    return;
                }
            }
            
            Assert.Fail("Did not receive deployment completion notification");
        }
        finally
        {
            await app.StopAsync();
        }
    }
} 