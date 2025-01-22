using a2k.Shared.Models;
using Spectre.Console;

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

    public static int GenerateRandomPort() => Random.Shared.Next(30000, 32768);

    public static string ToMarkup(this Enum resourceOperationResult)
        => resourceOperationResult switch
        {
            Outcome.Created => $"[bold lightgreen]{Outcome.Created}[/]",
            Outcome.Exists => $"[bold silver]{Outcome.Exists}[/]",
            Outcome.Replaced => $"[bold slateblue3]{Outcome.Replaced}[/]",
            Outcome.Updated => $"[bold lightcoral]{Outcome.Updated}[/]",
            Outcome.Deleted => $"[bold red3_1]{Outcome.Deleted}[/]",
            Outcome.Missing => $"[bold lightgoldenrod3]{Outcome.Missing}[/]",
            Outcome.Failed => $"[bold darkred]{Outcome.Failed}[/]",
            Outcome.Succeeded => $"[bold chartreuse2]{Outcome.Succeeded}[/]",
            _ => "???"
        };

    public static Markup ToMarkup(this Enum resourceOperationResult, string resourceName, Exception? ex = null)
        => resourceOperationResult switch
        {
            Outcome.Created => new Markup($"{Outcome.Created.ToMarkup()} [yellow]{resourceName}[/]"),
            Outcome.Exists => new Markup($"[yellow]{resourceName}[/] {Outcome.Exists.ToMarkup()}"),
            Outcome.Replaced => new Markup($"{Outcome.Replaced.ToMarkup()} [yellow]{resourceName}[/]"),
            Outcome.Updated => new Markup($"{Outcome.Updated.ToMarkup()} [yellow]{resourceName}[/]"),
            Outcome.Deleted => new Markup($"{Outcome.Deleted.ToMarkup()} [yellow]{resourceName}[/]"),
            Outcome.Missing => new Markup($"{Outcome.Missing.ToMarkup()} [yellow]{resourceName}[/]"),
            Outcome.Failed => new Markup($"{Outcome.Failed.ToMarkup()} [yellow]{resourceName}[/]: [dim]: {Markup.Escape(ex.Message)}[/]"),
            Outcome.Succeeded => new Markup($"[yellow]{resourceName}[/] {Outcome.Succeeded.ToMarkup()}"),
            _ => new("???")
        };
}
