using a2k;

namespace UnitTests;

public class ResourceRegistrationTests
{
    [Test]
    public async Task AddDeployment_RegistersKubernetesResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var deployment = builder.AddDeployment("test-deployment");

        await Assert.That(deployment.Resource).IsTypeOf<a2k.Models.Deployment>();
        await Assert.That(deployment.Resource.Name).IsEqualTo("test-deployment");
    }
}