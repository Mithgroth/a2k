using a2k.Deployment;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;

namespace a2k;

internal sealed class KubernetesLifecycleHook(DistributedApplicationExecutionContext executionContext,
                                              IServiceProvider serviceProvider,
                                              ResourceNotificationService notificationService,
                                              ResourceLoggerService loggerService) 
    : IDistributedApplicationLifecycleHook
{
    public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        var k8sResources = appModel.Resources.OfType<IKubernetesResource>().ToList();
        if (k8sResources.Count == 0)
        {
            return;
        }

        var context = serviceProvider.GetRequiredService<KubernetesContext>();
        var client = context.Client;

        try
        {
            await context.Client.CreateNamespaceAsync(new V1Namespace
            {
                Metadata = new V1ObjectMeta
                {
                    Name = context.Namespace,
                    Labels = context.CommonLabels
                }
            }, cancellationToken: cancellationToken);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
        {
            // Namespace already exists - this is acceptable
        }

        // Existing parent-child lookup and deployment logic
        var parentChildLookup = appModel.Resources.OfType<IResourceWithParent>()
            .Select(x => (Child: x, Root: x.Parent.TrySelectParentResource<IKubernetesResource>()))
            .Where(x => x.Root is not null)
            .ToLookup(x => x.Root, x => x.Child);

        _ = Task.Run(() => DeployKubernetesResourcesAsync(k8sResources, parentChildLookup, cancellationToken), cancellationToken);
    }

    private async Task DeployKubernetesResourcesAsync(
        List<IKubernetesResource> resources,
        ILookup<IKubernetesResource?, IResourceWithParent> parentChildLookup,
        CancellationToken cancellationToken)
    {
        var deployer = serviceProvider.GetRequiredService<KubernetesDeployer>();
        
        foreach (var resource in resources)
        {
            resource.DeploymentTaskCompletionSource = new TaskCompletionSource();
            
            try
            {
                await UpdateStateAsync(resource, parentChildLookup, s => s with
                {
                    State = new ResourceStateSnapshot("Deploying", KnownResourceStateStyles.Info)
                });

                await deployer.DeployAsync(resource, cancellationToken);

                await UpdateStateAsync(resource, parentChildLookup, s => s with
                {
                    State = new ResourceStateSnapshot("Deployed", KnownResourceStateStyles.Success)
                });
            }
            catch (Exception ex)
            {
                loggerService.GetLogger(resource).LogError(ex, "Error deploying Kubernetes resource {ResourceName}", resource.Name);
                await UpdateStateAsync(resource, parentChildLookup, s => s with
                {
                    State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error)
                });
            }
            finally
            {
                resource.DeploymentTaskCompletionSource?.TrySetResult();
            }
        }
    }

    private async Task UpdateStateAsync(IKubernetesResource resource,
                                        ILookup<IKubernetesResource?, IResourceWithParent> parentChildLookup,
                                        Func<CustomResourceSnapshot, CustomResourceSnapshot> stateFactory)
    {
        await notificationService.PublishUpdateAsync(resource, stateFactory);
        foreach (var child in parentChildLookup[resource])
        {
            await notificationService.PublishUpdateAsync(child, stateFactory);
        }
    }
} 