using a2k.Shared;
using a2k.Shared.Models.Aspire;
using System.Text.Json;

namespace ManifestLoading;

public class ManifestLoadingTests
{
    private readonly string TestManifestsPath = Path.Combine(AppContext.BaseDirectory, "TestManifests");

    public ManifestLoadingTests()
    {

    }

    [Theory]
    [InlineData("valid-manifest.json", 2)]
    [InlineData("missing-resource.json", 0)]
    public void LoadManifest_ShouldHandleVariousManifests(string fileName, int expectedResourceCount)
    {
        // Arrange
        var manifestPath = Path.Combine(TestManifestsPath, fileName);
        var json = File.ReadAllText(manifestPath);

        // Act
        var manifest = JsonSerializer.Deserialize<Manifest>(json, Defaults.JsonSerializerOptions);

        // Assert
        Assert.NotNull(manifest);
        Assert.Equal(expectedResourceCount, manifest.Resources.Count);
    }

    [Fact]
    public void LoadManifest_ShouldFailForInvalidManifest()
    {
        // Arrange
        var manifestPath = Path.Combine("TestManifests", "invalid-manifest.json");
        var json = File.ReadAllText(manifestPath);

        // Act & Assert
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<Manifest>(json, Defaults.JsonSerializerOptions);
        });
    }
}
