namespace a2k.Shared.Models;

public record Dockerfile(string Name,
                         string Tag = "latest",
                         string? Context = "",
                         string? Path = "",
                         bool ShouldBuildWithDocker = true)
{
    public Dockerfile(string Image)
        : this(ParseNameFromImage(Image), ParseTagFromImage(Image))
    {
        ShouldBuildWithDocker = false;
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
}
