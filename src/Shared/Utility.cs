namespace a2k.Shared;

public static class Utility
{
    public static string ToKebabCase(this string input) 
        => string.IsNullOrEmpty(input)
        ? input
        : string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x.ToString() : x.ToString()))
        .Trim('-')
        .ToLowerInvariant();
}
