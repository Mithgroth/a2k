using a2k;
using a2k.Annotations;

namespace UnitTests;

public class MetadataTests
{
    [Test]
    public async Task UseKubernetes_AddsManagedByAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var project = builder.AddProject<Projects.AspireApp1_ApiService>("test");
        
        builder.UseKubernetes();

        var annotation = project.Resource.Annotations
            .OfType<KubernetesAnnotation>()
            .FirstOrDefault(a => a.Name == "kubernetes.io/managed-by");

        await Assert.That(annotation?.Value).IsEqualTo("a2k");
    }
}