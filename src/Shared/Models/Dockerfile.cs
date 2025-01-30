using Spectre.Console;
using System.Text.Json;

namespace a2k.Shared.Models;

public record Dockerfile(string Name,
                         string Tag = "latest",
                         string? Context = "",
                         string? Path = "",
                         bool ShouldBuildWithDocker = true,
                         string? SHA256 = null)
{
    public string FullImageName => $"{Name}:{Tag}";

    public Dockerfile(string Image)
        : this(ParseNameFromImage(Image), ParseTagFromImage(Image))
    {
        ShouldBuildWithDocker = false;
        LoadSHA256();
    }

    private static string ParseNameFromImage(string image)
    {
        var parts = image.Split(':');
        return parts[0]; // Assume everything before ':' is the name
    }

    private static string ParseTagFromImage(string image)
    {
        var parts = image.Split(':');
        return parts.Length > 1 ? parts[1] : "latest"; // Default to "latest" if no tag is provided
    }

    public Dockerfile UpdateSHA256(string sha256)
    {
        var imageCache = File.Exists(Defaults.ImageCachePath)
            ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(Defaults.ImageCachePath))
            : [];

        // Initialize list if this is the first SHA256 for this image
        if (!imageCache.TryGetValue(Name, out var value))
        {
            imageCache[Name] = value ?? [];
        }

        // Add new SHA256 if it's not already in the list
        if (!imageCache[Name].Contains(sha256))
        {
            imageCache[Name].Add(sha256);
        }

        // Update with new SHA256
        var updated = this with { SHA256 = sha256 };
        updated.SaveSHA256();

        return updated;
    }

    private void SaveSHA256()
    {
        if (SHA256 == null || !ShouldBuildWithDocker)
        {
            return;
        }

        var cachePath = Defaults.ImageCachePath;
        var cacheDir = System.IO.Path.GetDirectoryName(cachePath);
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        var imageCache = File.Exists(Defaults.ImageCachePath)
            ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(Defaults.ImageCachePath))
            : [];

        if (!imageCache.TryGetValue(Name, out var value))
        {
            value = ([]);
            imageCache[Name] = value;
        }

        if (!value.Contains(SHA256))
        {
            value.Add(SHA256);
        }

        File.WriteAllText(cachePath, JsonSerializer.Serialize(imageCache, Defaults.JsonSerializerOptions));
    }

    private Dockerfile LoadSHA256()
    {
        if (!ShouldBuildWithDocker || !File.Exists(Defaults.ImageCachePath))
        {
            return this;
        }

        var imageCache = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(Defaults.ImageCachePath));
        if (imageCache?.TryGetValue(Name, out var sha256List) == true && sha256List.Count > 0)
        {
            return this with { SHA256 = sha256List[^1] };
        }

        return this;
    }
}
