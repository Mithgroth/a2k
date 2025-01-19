using System.Text.Json;

namespace a2k.Shared.Models;

public record Dockerfile(string Name,
                         string Tag = "latest",
                         string? Context = "",
                         string? Path = "",
                         bool ShouldBuildWithDocker = true,
                         string? SHA256 = null)
{
    private static string GetImageCachePath() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "a2k", "docker-images.json");

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
        var cachePath = GetImageCachePath();
        Dictionary<string, string> imageCache;
        try
        {
            imageCache = File.Exists(cachePath) 
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cachePath)) 
                : [];
        }
        catch
        {
            imageCache = [];
        }

        // Store the old SHA256 before updating
        string? oldSHA256 = null;
        imageCache?.TryGetValue(Name, out oldSHA256);

        // Update with new SHA256
        var updated = this with { SHA256 = sha256 };
        updated.SaveSHA256();

        // If we had an old SHA256 and it's different from the new one, clean it up
        if (oldSHA256 != null && oldSHA256 != sha256)
        {
            try
            {
                Shell.Run($"docker rmi {oldSHA256} --force");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        return updated;
    }

    private void SaveSHA256()
    {
        if (SHA256 == null || !ShouldBuildWithDocker)
        {
            return;
        }

        var cachePath = GetImageCachePath();
        var cacheDir = System.IO.Path.GetDirectoryName(cachePath);
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        Dictionary<string, string> imageCache;
        try
        {
            imageCache = File.Exists(cachePath) 
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cachePath)) 
                : [];
        }
        catch
        {
            imageCache = [];
        }

        imageCache ??= [];
        imageCache[Name] = SHA256;
        File.WriteAllText(cachePath, JsonSerializer.Serialize(imageCache, Defaults.JsonSerializerOptions));
    }

    private Dockerfile LoadSHA256()
    {
        if (!ShouldBuildWithDocker)
        {
            return this;
        }

        var cachePath = GetImageCachePath();
        if (!File.Exists(cachePath))
        {
            return this;
        }

        try
        {
            var imageCache = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cachePath));
            if (imageCache?.TryGetValue(Name, out var sha256) == true)
            {
                return this with { SHA256 = sha256 };
            }
        }
        catch
        {

        }

        return this;
    }
}
