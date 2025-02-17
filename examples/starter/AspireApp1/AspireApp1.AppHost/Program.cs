using a2k;
using a2k.Annotations;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice");

builder.AddProject<Projects.AspireApp1_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.UseKubernetes(options =>
{
    options.Namespace = "aspireapp1";
    options.CommonLabels["app.kubernetes.io/part-of"] = "aspireapp1";
});

builder.WithKubernetesResources(resource =>
{
    resource.WithAnnotation(new KubernetesAnnotation("kubernetes.io/deployment", resource.Resource.Name));
});

builder.Build().Run();
