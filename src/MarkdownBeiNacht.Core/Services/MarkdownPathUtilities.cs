namespace MarkdownBeiNacht.Core.Services;

public static class MarkdownPathUtilities
{
    private static readonly HashSet<string> MarkdownExtensions =
    [
        ".md",
        ".markdown",
        ".mdown",
    ];

    private static readonly HashSet<string> PlainTextExtensions =
    [
        ".txt",
    ];

    private static readonly HashSet<string> SupportedDocumentExtensions =
    [
        ..MarkdownExtensions,
        ..PlainTextExtensions,
    ];

    public static Uri? CreateBaseUri(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return new Uri(NormalizePath(filePath));
    }

    public static bool IsMarkdownPath(string? path) =>
        HasSupportedExtension(path, MarkdownExtensions);

    public static bool IsPlainTextPath(string? path) =>
        HasSupportedExtension(path, PlainTextExtensions);

    public static bool IsSupportedDocumentPath(string? path) =>
        HasSupportedExtension(path, SupportedDocumentExtensions);

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

    private static bool HasSupportedExtension(string? path, HashSet<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var withoutFragment = path.Split('#', '?')[0];
        return extensions.Contains(Path.GetExtension(withoutFragment).ToLowerInvariant());
    }
}

