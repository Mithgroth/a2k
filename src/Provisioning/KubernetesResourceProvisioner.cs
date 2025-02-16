using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

internal abstract class KubernetesResourceProvisioner<T>(ResourceLoggerService loggerService, ResourceNotificationService notificationService) 
    : IResourceProvisioner<T> where T : IKubernetesResource
{
    // Explicit interface implementation for non-generic provisioner
    async Task IResourceProvisioner.ProvisionAsync(IKubernetesResource resource, CancellationToken cancellationToken)
        => await ProvisionAsync((T)resource, cancellationToken);

    public async Task ProvisionAsync(T resource, CancellationToken cancellationToken)
    {
        try 
        {
            await ApplyResourceAsync(resource, cancellationToken);
            await UpdateStateAsync(resource, "Running", KnownResourceStateStyles.Success);
        }
        catch (Exception ex)
        {
            loggerService.GetLogger(resource).LogError(ex, "Error provisioning {ResourceName}", resource.Name);
            await UpdateStateAsync(resource, "Failed", KnownResourceStateStyles.Error);
            throw;
        }
    }

    protected abstract Task ApplyResourceAsync(T resource, CancellationToken cancellationToken);
    
    private async Task UpdateStateAsync(T resource, string state, string style)
    {
        await notificationService.PublishUpdateAsync(resource, s => s with {
            State = new ResourceStateSnapshot(state, style)
        });
    }
} 