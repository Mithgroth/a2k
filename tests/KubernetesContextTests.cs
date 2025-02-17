using a2k;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests;

public class KubernetesContextTests
{
    [Test]
    public async Task UseKubernetes_RegistersKubernetesContext()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        builder.UseKubernetes(options =>
        {
            options.Namespace = "test";
            options.CommonLabels["environment"] = "test";
        });

        using var app = builder.Build();
        var context = app.Services.GetRequiredService<KubernetesContext>();
        
        await Assert.That(context).IsNotNull();
        await Assert.That(context.Namespace).IsEqualTo("test");
        await Assert.That(context.CommonLabels["environment"]).IsEqualTo("test");
    }
} 