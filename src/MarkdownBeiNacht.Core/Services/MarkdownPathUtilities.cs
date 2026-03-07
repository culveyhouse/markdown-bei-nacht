namespace MarkdownBeiNacht.Core.Services;

public static class MarkdownPathUtilities
{
    private static readonly HashSet<string> MarkdownExtensions =
    [
        ".md",
        ".markdown",
        ".mdown",
    ];

    public static Uri? CreateBaseUri(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return new Uri(NormalizePath(filePath));
    }

    public static bool IsMarkdownPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var withoutFragment = path.Split('#', '?')[0];
        return MarkdownExtensions.Contains(Path.GetExtension(withoutFragment).ToLowerInvariant());
    }

    public static string NormalizePath(string path)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(expanded);
        }
        catch (Exception)
        {
            return path.Trim();
        }
    }
}


