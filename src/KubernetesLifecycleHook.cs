using a2k.Provisioning;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace a2k;

internal sealed class KubernetesLifecycleHook(
    DistributedApplicationExecutionContext executionContext,
    IServiceProvider serviceProvider,
    ResourceNotificationService notificationService,
    ResourceLoggerService loggerService) : IDistributedApplicationLifecycleHook
{
    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        var k8sResources = appModel.Resources.OfType<IKubernetesResource>().ToList();
        if (k8sResources.Count == 0)
        {
            return Task.CompletedTask;
        }

        var parentChildLookup = appModel.Resources.OfType<IResourceWithParent>()
            .Select(x => (Child: x, Root: x.Parent.TrySelectParentResource<IKubernetesResource>()))
            .Where(x => x.Root is not null)
            .ToLookup(x => x.Root, x => x.Child);

        _ = Task.Run(() => ProvisionKubernetesResourcesAsync(k8sResources, parentChildLookup, cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task ProvisionKubernetesResourcesAsync(
        List<IKubernetesResource> resources,
        ILookup<IKubernetesResource?, IResourceWithParent> parentChildLookup,
        CancellationToken cancellationToken)
    {
        foreach (var resource in resources)
        {
            resource.ProvisioningTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            
            try
            {
                await UpdateStateAsync(resource, parentChildLookup, s => s with
                {
                    State = new ResourceStateSnapshot("Provisioning", KnownResourceStateStyles.Info)
                });

                var provisioner = serviceProvider.GetKeyedService<IResourceProvisioner>(resource.GetType());
                if (provisioner != null)
                {
                    await provisioner.ProvisionAsync(resource, cancellationToken);
                }

                await UpdateStateAsync(resource, parentChildLookup, s => s with
                {
                    State = new ResourceStateSnapshot("Running", KnownResourceStateStyles.Success)
                });
            }
            catch (Exception ex)
            {
                loggerService.GetLogger(resource).LogError(ex, "Error provisioning Kubernetes resource {ResourceName}", resource.Name);
                await UpdateStateAsync(resource, parentChildLookup, s => s with
                {
                    State = new ResourceStateSnapshot("Failed", KnownResourceStateStyles.Error)
                });
            }
            finally
            {
                resource.ProvisioningTaskCompletionSource?.TrySetResult();
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