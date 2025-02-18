using a2k.Annotations;
using a2k.Deployment;
using a2k.Models;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;

namespace a2k;

public static class KubernetesBuilderExtensions
{
    public static IDistributedApplicationBuilder UseKubernetes(
        this IDistributedApplicationBuilder builder,
        Action<KubernetesOptions>? configureOptions = null)
    {
        var options = new KubernetesOptions();
        configureOptions?.Invoke(options);
        builder.Services.AddSingleton(options);

        var context = new KubernetesContext(options);
        builder.Services.AddSingleton(context);
        builder.Services.AddSingleton<KubernetesDeployer>();
        builder.Services.TryAddLifecycleHook<KubernetesLifecycleHook>();

        foreach (var resource in builder.Resources.OfType<IKubernetesResource>())
        {
            resource.Metadata.NamespaceProperty = options.Namespace;
            resource.Annotations.Add(new KubernetesAnnotation("kubernetes.io/managed-by", "a2k"));
        }

        return builder;
    }

    public static IDistributedApplicationBuilder WithKubernetesResources(this IDistributedApplicationBuilder builder,
                                                                         Action<IResourceBuilder<IResource>> configureResource)
    {
        foreach (var resource in builder.Resources)
        {
            var resourceBuilder = builder.CreateResourceBuilder(resource);
            configureResource(resourceBuilder);
        }

        return builder;
    }

    public static IResourceBuilder<T> WithKubernetesMetadata<T>(this IResourceBuilder<T> builder,
                                                                Action<V1ObjectMeta> configureMetadata) 
        where T : IKubernetesResource
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
        var options = builder.ApplicationBuilder.Services
            .BuildServiceProvider()
            .GetRequiredService<KubernetesContext>();

        // Get Aspire's Dockerfile build annotation if present
        var dockerfileAnnotation = builder.Resource.Annotations
            .OfType<DockerfileBuildAnnotation>()
            .FirstOrDefault();

        string imageName;
        if (dockerfileAnnotation is not null)
        {
            // Use Aspire's generated image name from WithDockerfile()
            var containerImageAnnotation = builder.Resource.Annotations
                .OfType<ContainerImageAnnotation>()
                .First();
            
            imageName = $"{containerImageAnnotation.Image}:{containerImageAnnotation.Tag}";
        }
        else
        {
            // Fallback to Aspire's default project containerization
            imageName = $"{builder.Resource.Name.ToLowerInvariant()}:latest";
            builder.PublishAsDockerFile(); // From Aspire's ExecutableResourceBuilderExtensions
        }

        var deployment = new Models.Deployment(
            name: builder.Resource.Name,
            @namespace: options.Namespace
        );
        
        deployment.Spec.Template.Spec.Containers[0].Image = imageName;
        
        // Add image pull secrets if needed
        deployment.Spec.Template.Spec.ImagePullSecrets = new List<V1LocalObjectReference>
        {
            new V1LocalObjectReference { Name = "registry-credentials" }
        };

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