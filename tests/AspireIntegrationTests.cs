using a2k;
using a2k.Annotations;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace IntegrationTests;

public class AspireIntegrationTests
{
    [Test]
    public async Task WeatherForecastApi_ReturnsValidData()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true
        });

        var cache = builder.AddRedis("cache");
        var apiService = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice");

        using var app = builder.Build();
        await app.StartAsync();

        try
        {
            var client = app.CreateHttpClient(apiService);
            var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");
            
            await Assert.That(forecasts).IsNotNull();
            await Assert.That(forecasts!.Length).IsEqualTo(5);
            await Assert.That(forecasts.All(f => f.Date > DateOnly.FromDateTime(DateTime.Now))).IsTrue();
            await Assert.That(forecasts.All(f => f.TemperatureC is >= (-20) and <= 55)).IsTrue();
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Test]
    public async Task WebFrontend_CanAccessWeatherApi()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true
        });

        var cache = builder.AddRedis("cache");
        var apiService = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice");
        var webApp = builder.AddProject<Projects.AspireApp1_Web>("webfrontend")
            .WithReference(cache)
            .WithReference(apiService);

        using var app = builder.Build();
        await app.StartAsync();

        try
        {
            var client = app.CreateHttpClient(webApp);
            var response = await client.GetAsync("/weather");
            
            await Assert.That(response.IsSuccessStatusCode).IsTrue();
            var content = await response.Content.ReadAsStringAsync();
            await Assert.That(content).Contains("Weather");
            await Assert.That(content).Contains("Loading...");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Test]
    public async Task ApiService_DeploysToKubernetes()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true
        });

        var apiService = builder.AddProject<Projects.AspireApp1_ApiService>("apiservice");

        builder.UseKubernetes(options =>
        {
            options.Namespace = "test";
        });

        builder.WithKubernetesResources(resource =>
        {
            resource.WithAnnotation(new KubernetesAnnotation("kubernetes.io/deployment", resource.Resource.Name));
        });

        using var app = builder.Build();
        await app.StartAsync();

        try
        {
            var k8sAnnotation = apiService.Resource.Annotations
                .OfType<KubernetesAnnotation>()
                .FirstOrDefault(a => a.Name == "kubernetes.io/deployment");
            
            await Assert.That(k8sAnnotation).IsNotNull();
            await Assert.That(k8sAnnotation!.Value).IsEqualTo("apiservice");
            await Assert.That(apiService.Resource).IsTypeOf<ProjectResource>();

        }
        finally
        {
            await app.StopAsync();
        }
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public static class TestExtensions 
{
    public static HttpClient CreateHttpClient(this DistributedApplication app, IResourceBuilder<ProjectResource> resource)
    {
        var serviceConfig = resource.Resource.GetEndpoint("http");
        return new HttpClient { BaseAddress = new Uri($"http://localhost:{serviceConfig.Port}") };
    }
} 