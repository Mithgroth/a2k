namespace a2k.Shared;

public static class Utility
{
    public static string ToKebabCase(this string input) 
        => string.IsNullOrEmpty(input)
        ? input
        : string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x.ToString() : x.ToString()))
        .Trim('-')
        .ToLowerInvariant();

    public static string FindAndFormatSolutionName(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            var solutionFile = directory.GetFiles("*.sln").FirstOrDefault();
            if (solutionFile != null)
            {
                return Path.GetFileNameWithoutExtension(solutionFile.Name).ToKebabCase();
            }

            directory = directory.Parent;
        }

        return "aspire-app";
    }

    public static string GenerateVersion()
    {
        var now = DateTime.UtcNow;
        var year = now.Year.ToString().Substring(2, 2);
        var dayOfYear = now.DayOfYear.ToString("D3");
        var time = now.ToString("HHmmss");
        
        return $"{year}{dayOfYear}{time}";
    }
}
