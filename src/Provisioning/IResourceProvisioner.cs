internal interface IResourceProvisioner
{
    Task ProvisionAsync(IKubernetesResource resource, CancellationToken cancellationToken = default);
}

internal interface IResourceProvisioner<T> : IResourceProvisioner where T : IKubernetesResource
{
    Task ProvisionAsync(T resource, CancellationToken cancellationToken = default);
}

internal abstract class ResourceProvisioner<T> : IResourceProvisioner<T> where T : IKubernetesResource
{
    public async Task ProvisionAsync(IKubernetesResource resource, CancellationToken cancellationToken)
        => await ProvisionAsync((T)resource, cancellationToken);

    public abstract Task ProvisionAsync(T resource, CancellationToken cancellationToken);
} 