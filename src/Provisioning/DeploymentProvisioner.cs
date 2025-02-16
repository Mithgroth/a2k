using Aspire.Hosting.ApplicationModel;

internal sealed class DeploymentProvisioner(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService)
    : KubernetesResourceProvisioner<Deployment>(loggerService, notificationService)
{
    protected override async Task ApplyResourceAsync(Deployment resource, CancellationToken cancellationToken)
    {
        using var client = new k8s.Kubernetes(resource.KubernetesConfig);
        await resource.ApplyAsync(client, cancellationToken);
    }
}
