using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public static class CommandLineParser
{
    public static StartupOptions Parse(IEnumerable<string> arguments)
    {
        var rawArgument = arguments.FirstOrDefault(argument => !string.IsNullOrWhiteSpace(argument));
        if (string.IsNullOrWhiteSpace(rawArgument))
        {
            return new StartupOptions(null, null);
        }

        var expandedArgument = Environment.ExpandEnvironmentVariables(rawArgument.Trim());
        if (Uri.TryCreate(expandedArgument, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            return new StartupOptions(
                MarkdownPathUtilities.NormalizePath(absoluteUri.LocalPath),
                NormalizeAnchor(absoluteUri.Fragment));
        }

        return new StartupOptions(MarkdownPathUtilities.NormalizePath(expandedArgument), null);
    }

    private static string? NormalizeAnchor(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        var normalized = fragment.TrimStart('#');
        return string.IsNullOrWhiteSpace(normalized) ? null : Uri.UnescapeDataString(normalized);
    }
}

