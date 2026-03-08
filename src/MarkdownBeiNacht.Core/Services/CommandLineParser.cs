using System;
using System.Globalization;
using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public static class CommandLineParser
{
    public static StartupOptions Parse(IEnumerable<string> arguments)
    {
        string? filePath = null;
        string? anchor = null;
        double? windowLeft = null;
        double? windowTop = null;

        foreach (var rawArgument in arguments.Where(argument => !string.IsNullOrWhiteSpace(argument)))
        {
            var argument = Environment.ExpandEnvironmentVariables(rawArgument.Trim());
            if (TryParseWindowCoordinate(argument, "--window-left", out var parsedLeft))
            {
                windowLeft = parsedLeft;
                continue;
            }

            if (TryParseWindowCoordinate(argument, "--window-top", out var parsedTop))
            {
                windowTop = parsedTop;
                continue;
            }

            if (filePath is not null)
            {
                continue;
            }

            if (Uri.TryCreate(argument, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
            {
                filePath = MarkdownPathUtilities.NormalizePath(absoluteUri.LocalPath);
                anchor = NormalizeAnchor(absoluteUri.Fragment);
                continue;
            }

            filePath = MarkdownPathUtilities.NormalizePath(argument);
        }

        return new StartupOptions(filePath, anchor, windowLeft, windowTop);
    }

    private static bool TryParseWindowCoordinate(string argument, string optionName, out double value)
    {
        value = default;
        if (argument.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        var rawValue = argument[(optionName.Length + 1)..];
        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
