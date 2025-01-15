using a2k.Cli.Models;
using System.Text.Json;

namespace Manifest;

public class ManifestLoaderTests
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string TestManifestsPath = Path.Combine(AppContext.BaseDirectory, "TestManifests");

    public ManifestLoaderTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
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
        var manifest = JsonSerializer.Deserialize<AspireManifest>(json, _jsonOptions);

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
            JsonSerializer.Deserialize<AspireManifest>(json, _jsonOptions);
        });
    }
}
