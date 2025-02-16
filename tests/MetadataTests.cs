using a2k;
using a2k.Annotations;

namespace UnitTests;

public class MetadataTests
{
    [Test]
    public async Task WithKubernetesMetadata_AddsManagedByAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var deployment = builder.AddDeployment("test")
            .WithKubernetesMetadata(_ => { });

        var annotation = deployment.Resource.Annotations
            .OfType<KubernetesAnnotation>()
            .FirstOrDefault(a => a.Name == "app.kubernetes.io/managed-by");

        await Assert.That(annotation?.Value).IsEqualTo("a2k");
    }
}