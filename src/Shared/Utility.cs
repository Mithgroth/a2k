using a2k.Shared.Models;
using Spectre.Console;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
            Outcome.Created => $"[bold lightgreen]{Emoji.Known.Plus} {Outcome.Created}[/]",
            Outcome.Exists => $"[bold silver]{Emoji.Known.EightSpokedAsterisk}  {Outcome.Exists}[/]",
            Outcome.Replaced => $"[bold slateblue3]{Emoji.Known.LargeBlueDiamond} {Outcome.Replaced}[/]",
            Outcome.Updated => $"[bold lightcoral]{Emoji.Known.LargeOrangeDiamond} {Outcome.Updated}[/]",
            Outcome.Deleted => $"[bold red3_1]{Emoji.Known.CrossMark} {Outcome.Deleted}[/]",
            Outcome.Missing => $"[bold lightgoldenrod3]{Emoji.Known.MagnifyingGlassTiltedRight} {Outcome.Missing}[/]",
            Outcome.Failed => $"[bold darkred]{Emoji.Known.Collision} {Outcome.Failed}[/]",
            Outcome.Succeeded => $"[bold chartreuse2]{Emoji.Known.ThumbsUp} {Outcome.Succeeded}[/]",
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

    public static int ExtractPort(string url)
    {
        var match = Regex.Match(url, @":(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
        {
            return port;
        }

        throw new InvalidOperationException("Invalid port format in launch settings");
    }

    public static int GenerateAvailablePort()
    {
        while (true)
        {
            var port = Random.Shared.Next(30000, 40000);

            var activeTcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            if (!activeTcpListeners.Any(ep => ep.Port == port))
            {
                return port;
            }
        }
    }

    private static readonly string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
    public static string GenerateSecureString(int minLength)
    {
        var length = Math.Max(minLength, 22); // Ensure minimum length of 22 for security
        var bytes = new byte[length];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var chars = bytes
            .Select(b => AllowedChars[b % AllowedChars.Length])
            .ToArray();

        return new string(chars);
    }
}
