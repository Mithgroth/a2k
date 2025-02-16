using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using k8s.Models;

public static class KubernetesBuilderExtensions
{
    public static IResourceBuilder<T> WithKubernetesMetadata<T>(this IResourceBuilder<T> builder, Action<V1ObjectMeta> configureMetadata) where T : Resource
    {
        builder.Resource.Annotations.Add(new KubernetesAnnotation("app.kubernetes.io/managed-by", "a2k"));
        configureMetadata?.Invoke(builder.Resource.Metadata);
        return builder;
    }

    public static IResourceBuilder<Deployment> AddDeployment(
        this IDistributedApplicationBuilder builder,
        string name,
        Action<V1Deployment>? configureDeployment = null)
    {
        var deployment = new Deployment(name);
        var resourceBuilder = builder.AddResource(deployment);
        
        if (configureDeployment is not null)
        {
            deployment.Configure(configureDeployment);
        }

        return resourceBuilder.WithKubernetesMetadata(metadata => {});
    }

    public static IResourceBuilder<HelmChart> AddHelmChart(
        this IDistributedApplicationBuilder builder,
        string name,
        string chartName,
        string version)
    {
        // Implementation of AddHelmChart method
        throw new NotImplementedException();
    }
} 