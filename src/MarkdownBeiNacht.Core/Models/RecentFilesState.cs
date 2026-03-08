using MarkdownBeiNacht.Core.Services;

namespace MarkdownBeiNacht.Core.Models;

public sealed record RecentFilesState(string[] Files)
{
    public const int MaxEntries = 8;

    public static RecentFilesState Empty { get; } = new([]);

    public RecentFilesState Normalize()
    {
        var normalized = (Files ?? [])
            .Where(MarkdownPathUtilities.IsSupportedDocumentPath)
            .Select(MarkdownPathUtilities.NormalizePath)
            .Where(path => string.IsNullOrWhiteSpace(path) is false)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxEntries)
            .ToArray();

        return new RecentFilesState(normalized);
    }
}
