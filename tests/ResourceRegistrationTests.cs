using Aspire.Hosting;

namespace UnitTests;

public class ResourceRegistrationTests
{
    [Test]
    public async Task AddDeployment_RegistersKubernetesResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var deployment = builder.AddDeployment("test-deployment");

        await Assert.That(deployment.Resource).IsTypeOf<Deployment>();
        await Assert.That(deployment.Resource.Name).IsEqualTo("test-deployment");
    }
}