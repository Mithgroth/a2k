using a2k.Annotations;
using a2k.Models;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using k8s.Models;

namespace a2k;

public static class KubernetesBuilderExtensions
{
    public static IDistributedApplicationBuilder UseKubernetes(
        this IDistributedApplicationBuilder builder,
        Action<KubernetesOptions>? configureOptions = null)
    {
        var options = new KubernetesOptions();
        configureOptions?.Invoke(options);

        foreach (var resource in builder.Resources.OfType<IResourceWithEnvironment>())
        {
            resource.Annotations.Add(new KubernetesAnnotation("kubernetes.io/managed-by", "a2k"));
        }

        return builder;
    }

    public static IDistributedApplicationBuilder WithKubernetesResources(
        this IDistributedApplicationBuilder builder,
        Action<IResourceBuilder<IResource>> configureResource)
    {
        foreach (var resource in builder.Resources)
        {
            var resourceBuilder = builder.CreateResourceBuilder(resource);
            configureResource(resourceBuilder);
        }

        return builder;
    }

    public static IResourceBuilder<T> WithKubernetesMetadata<T>(this IResourceBuilder<T> builder, Action<V1ObjectMeta> configureMetadata) where T : IKubernetesResource
    {
        builder.Resource.Annotations.Add(new KubernetesAnnotation("app.kubernetes.io/managed-by", "a2k"));
        configureMetadata?.Invoke(builder.Resource.Metadata);
        return builder;
    }

    public static IResourceBuilder<HelmChart> AddHelmChart(this IDistributedApplicationBuilder builder,
                                                           string name,
                                                           string chartName,
                                                           string version)
    {
        // Implementation of AddHelmChart method
        throw new NotImplementedException();
    }

    public static async Task<IResourceBuilder<ProjectResource>> ToKubernetes<ProjectResource>(
            this IResourceBuilder<ProjectResource> builder,
            Action<V1Deployment>? configureDeployment = null) 
        where ProjectResource : IResourceWithEnvironment
    {
        var deployment = new Deployment(builder.Resource.Name);
        
        // Copy environment variables and other Aspire configurations
        var envVars = await builder.Resource.GetEnvironmentVariableValuesAsync();
        foreach (var env in envVars)
        {
            deployment.Spec.Template.Spec.Containers[0].Env.Add(new V1EnvVar(env.Key, env.Value));
        }

        configureDeployment?.Invoke(deployment);
        
        // Add the deployment as a child resource
        builder.WithAnnotation(new KubernetesAnnotation("kubernetes.io/deployment", deployment.Name));
        
        return builder;
    }
}

public class KubernetesOptions
{
    public string? Namespace { get; set; }
    public Dictionary<string, string> CommonLabels { get; set; } = new();
}