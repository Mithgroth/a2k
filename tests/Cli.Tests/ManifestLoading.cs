using a2k.Shared;
using a2k.Shared.Models;
using a2k.Shared.Models.Aspire;
using System.Text.Json;

namespace ManifestLoading;

public class Test
{
    private readonly string TestManifestsPath = Path.Combine(AppContext.BaseDirectory, "TestManifests");

    public Test()
    {

    }

    [Theory]
    [InlineData("valid-manifest.json", 2, ResourceOperationResult.Succeeded)]
    [InlineData("missing-resource.json", 0, ResourceOperationResult.Failed)]
    // TODO: Add .Missing case test
    public async Task LoadManifests(string fileName,
                                    int expectedResourceCount,
                                    ResourceOperationResult expectedResult)
    {
        // Arrange
        var manifestPath = Path.Combine(TestManifestsPath, fileName);
        var solution = new Solution("test", "test", "test")
        {
            ManifestPath = manifestPath
        };

        // Act
        var result = await solution.ReadManifest();

        // Assert
        Assert.Equal(expectedResourceCount, solution.Resources.Count);
        Assert.Equal(expectedResult, result.OperationResult);
    }

    [Fact]
    public void FailInvalidManifest()
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
